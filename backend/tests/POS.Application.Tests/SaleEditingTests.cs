using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Reports;
using POS.Application.Sales;
using POS.Application.Shifts;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Domain.Events;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class SaleEditingTests
{
    [Theory]
    [InlineData("Cash", 10, null, null, 10, 0)]
    [InlineData("Card", 10, null, null, 0, 10)]
    [InlineData("Mixed", 10, 4d, 6d, 4, 6)]
    public void Payment_allocation(string method, double total, double? cash, double? card, double expectedCash, double expectedCard)
    {
        var actual = SalePaymentCalculator.Calculate(method, (decimal)total, (decimal?)cash, (decimal?)card);
        Assert.Equal(((decimal)expectedCash, (decimal)expectedCard), actual);
    }

    [Theory]
    [InlineData("Mixed", 0, 10)]
    [InlineData("Mixed", 10, 0)]
    [InlineData("Mixed", 4, 7)]
    [InlineData("Mixed", -1, 11)]
    [InlineData("Mixed", 4.0001, 5.9999)]
    [InlineData("Cash", 4, 6)]
    [InlineData("Card", 4, 6)]
    [InlineData("Other", 4, 6)]
    public void Invalid_payment_is_rejected(string method, double cash, double card)
        => Assert.Throws<ValidationException>(() => SalePaymentCalculator.Calculate(method, 10, (decimal)cash, (decimal)card));

    [Fact]
    public async Task Edit_preserves_identity_price_and_recipe_and_records_audit()
    {
        await using var f = await Fixture.Create();
        var original = await f.Service.CreateAsync(f.Request(2));
        f.Product.Price = 99;
        f.Recipe.QuantityRequired = 20;
        await f.Db.SaveChangesAsync();
        var updated = await f.Service.UpdateAsync(original.Id, new(f.Request(3, "Mixed", 4, 26), "Correct quantity", original.Revision));
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(original.CreatedAt, updated.CreatedAt);
        Assert.Equal(original.CashierUserId, updated.CashierUserId);
        Assert.Equal(30, updated.TotalAmount);
        Assert.Equal(4, updated.CashAmount);
        Assert.Equal(26, updated.CardAmount);
        Assert.Equal(94, f.Stock.CurrentQuantity);
        Assert.Equal(1, updated.Revision);
        var audit = Assert.Single(await f.Service.HistoryAsync(original.Id));
        Assert.Equal("Test cashier", audit.EditedByName);
        Assert.Equal(20, audit.Before.TotalAmount);
        Assert.Equal(30, audit.After.TotalAmount);
        Assert.Equal("Correct quantity", audit.Reason);
        Assert.Equal(1, await f.Db.Sales.CountAsync());
        Assert.Single(await f.Db.SaleItems.ToListAsync());
    }

    [Fact]
    public async Task Mixed_sales_count_once_and_only_cash_enters_till()
    {
        await using var f = await Fixture.Create();
        await f.Service.CreateAsync(f.Request(1, "Mixed", 4, 6));
        var report = await new ReportService(f.Db, f.User).GetDailyBranchAsync(f.BranchId, DateOnly.FromDateTime(DateTime.UtcNow.AddHours(4)));
        Assert.Equal(1, report.InvoiceCount);
        Assert.Equal(10, report.TotalSales);
        Assert.Equal(4, report.PaymentBreakdown.Single(p => p.PaymentMethod == PaymentMethods.Cash).TotalAmount);
        Assert.Equal(6, report.PaymentBreakdown.Single(p => p.PaymentMethod == PaymentMethods.Card).TotalAmount);
        var shift = await new ShiftService(f.Db, f.User).GetCurrentAsync();
        Assert.NotNull(shift);
        Assert.Equal(54, shift.ClosingCashExpected);
    }

    [Fact]
    public async Task Cashier_cannot_edit_another_cashiers_sale_but_branch_manager_can()
    {
        await using var f = await Fixture.Create();
        var original = await f.Service.CreateAsync(f.Request());
        f.User.UserId = Guid.NewGuid();
        f.Db.Users.Add(new User { Id = f.User.UserId.Value, FullName = "Manager", BranchId = f.BranchId });
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.UpdateAsync(original.Id, new(f.Request(), "Fix", 0)));
        Assert.Empty(await f.Service.ListAsync(f.BranchId));
        f.User.RoleName = RoleNames.BranchManager;
        var updated = await f.Service.UpdateAsync(original.Id, new(f.Request(2), "Manager fix", 0));
        Assert.Equal(original.CashierUserId, updated.CashierUserId);
    }

    [Fact]
    public async Task Edits_require_permission_reason_open_shift_and_current_revision()
    {
        await using var f = await Fixture.Create();
        var sale = await f.Service.CreateAsync(f.Request());
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.UpdateAsync(sale.Id, new(f.Request(), " ", 0)));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => f.Service.UpdateAsync(sale.Id, new(f.Request(), "Fix", 3)));
        f.User.Permissions = [];
        await Assert.ThrowsAsync<ForbiddenException>(() => f.Service.UpdateAsync(sale.Id, new(f.Request(), "Fix", 0)));
        f.User.Permissions = [PermissionKeys.SalesEdit];
        f.Shift.Status = ShiftStatus.Closed;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ValidationException>(() => f.Service.UpdateAsync(sale.Id, new(f.Request(), "Fix", 0)));
        Assert.Empty(await f.Service.HistoryAsync(sale.Id));
    }

    [Fact]
    public async Task Cross_branch_edits_and_history_are_rejected()
    {
        await using var f = await Fixture.Create();
        var sale = await f.Service.CreateAsync(f.Request());
        f.User.BranchId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.UpdateAsync(sale.Id, new(f.Request(), "Fix", 0)));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.HistoryAsync(sale.Id));
    }

    [Fact]
    public async Task Payment_only_edit_does_not_change_inventory_and_void_restores_actual_consumption()
    {
        await using var f = await Fixture.Create();
        f.Stock.CurrentQuantity = 1;
        await f.Db.SaveChangesAsync();
        var sale = await f.Service.CreateAsync(f.Request());
        Assert.Equal(0, f.Stock.CurrentQuantity);
        await f.Service.UpdateAsync(sale.Id, new(f.Request(1, "Mixed", 4, 6), "Change payment", 0));
        Assert.Equal(0, f.Stock.CurrentQuantity);
        await new VoidService(f.Db, f.User).VoidAsync(sale.Id, new("Cancelled"));
        Assert.Equal(1, f.Stock.CurrentQuantity);
    }

    [Fact]
    public async Task Replacing_items_restores_stock_and_rejects_stale_edits()
    {
        await using var f = await Fixture.Create();
        var other = new Product { Id = Guid.NewGuid(), NameAr = "Food", NameEn = "Food", Price = 5 };
        f.Db.Products.Add(other);
        await f.Db.SaveChangesAsync();
        var sale = await f.Service.CreateAsync(f.Request(2));
        var request = f.Request() with { Lines = [new(other.Id, 3)] };
        var updated = await f.Service.UpdateAsync(sale.Id, new(request, "Replace item", 0));
        Assert.Equal(15, updated.TotalAmount);
        Assert.Equal(100, f.Stock.CurrentQuantity);
        Assert.Equal(other.Id, Assert.Single(updated.Items).ProductId);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => f.Service.UpdateAsync(sale.Id, new(request, "Stale edit", 0)));
        Assert.Single(await f.Service.HistoryAsync(sale.Id));
    }

    [Fact]
    public async Task Discounts_are_preserved_when_changing_payment()
    {
        await using var f = await Fixture.Create();
        var request = f.Request(2, "Mixed", 5, 4) with { DiscountType = "Percentage", DiscountValue = 50,
            Lines = [new(f.Product.Id, 2, "FixedAmount", 2)] };
        var sale = await f.Service.CreateAsync(request);
        Assert.Equal(9, sale.TotalAmount);
        Assert.Equal(11, sale.DiscountAmount);
        var changed = await f.Service.UpdateAsync(sale.Id, new(request with { PaymentMethod = "Card", CashAmount = 0, CardAmount = 9 }, "Payment correction", 0));
        Assert.Equal(9, changed.TotalAmount);
        Assert.Equal(96, f.Stock.CurrentQuantity);
    }

    private sealed class TestUser : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public Guid? BranchId { get; set; } = Guid.NewGuid();
        public string? RoleName { get; set; } = RoleNames.Cashier;
        public IReadOnlyCollection<string> Permissions { get; set; } = [PermissionKeys.SalesCreate, PermissionKeys.SalesEdit];
        public bool BypassBranchFilter => false;
    }
    private sealed class Events : IDomainEventPublisher
    {
        public Task PublishAsync<T>(T domainEvent, CancellationToken cancellationToken = default) where T : IDomainEvent => Task.CompletedTask;
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public TestUser User { get; } = new();
        public AppDbContext Db { get; }
        public SaleService Service { get; }
        public Guid BranchId { get; }
        public Product Product { get; } = new() { Id = Guid.NewGuid(), NameAr = "Tea", NameEn = "Tea", Price = 10 };
        public Shift Shift { get; }
        public BranchRawMaterialStock Stock { get; }
        public ProductRecipe Recipe { get; }
        private Fixture()
        {
            BranchId = User.BranchId!.Value;
            Db = new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, User);
            Service = new(Db, new Events(), User);
            Shift = new() { Id = Guid.NewGuid(), BranchId = BranchId, CashierUserId = User.UserId!.Value, OpeningCash = 50, OpenedAt = DateTime.UtcNow };
            var materialId = Guid.NewGuid();
            Stock = new() { BranchId = BranchId, RawMaterialId = materialId, CurrentQuantity = 100 };
            Recipe = new() { BranchId = BranchId, ProductId = Product.Id, RawMaterialId = materialId, QuantityRequired = 2 };
            Db.Branches.Add(new() { Id = BranchId, NameAr = "Branch", NameEn = "Branch", Code = "test" });
            Db.Users.Add(new() { Id = User.UserId.Value, FullName = "Test cashier", BranchId = BranchId });
            Db.SalesChannels.Add(new() { Id = SalesChannelIds.InStore, NameAr = "Store", NameEn = "Store", IsActive = true, IsInStore = true });
            Db.Products.Add(Product); Db.Shifts.Add(Shift); Db.BranchRawMaterialStocks.Add(Stock); Db.ProductRecipes.Add(Recipe);
        }
        public static async Task<Fixture> Create() { var f = new Fixture(); await f.Db.SaveChangesAsync(); return f; }
        public CreateSaleRequest Request(decimal quantity = 1, string method = "Cash", decimal? cash = null, decimal? card = null)
            => new(BranchId, method, [new(Product.Id, quantity)], CashAmount: cash, CardAmount: card);
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
