using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Reports;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class RestaurantReportTests
{
    [Fact]
    public async Task Dashboard_uses_paid_restaurant_orders_payments_channels_and_order_types()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        var branch = new Branch { Id = Guid.NewGuid(), Code = "B1", NameAr = "الفرع", NameEn = "Branch" };
        var category = new MenuCategory { Id = Guid.NewGuid(), NameAr = "وجبات", NameEn = "Meals" };
        var item = new MenuItem { Id = Guid.NewGuid(), Category = category, NameAr = "وجبة", NameEn = "Meal", Kind = MenuItemKinds.Combo, BasePrice = 5 };
        var type = await db.OrderTypes.SingleAsync(x => x.Code == "TAKEAWAY", TestContext.Current.CancellationToken);
        var channel = new SalesChannel { Id = Guid.NewGuid(), Code = "IN_STORE_REPORT", NameAr = "المتجر", NameEn = "In store", IsActive = true };
        var cash = await db.PaymentMethods.SingleAsync(x => x.Code == "CASH", TestContext.Current.CancellationToken);
        var card = await db.PaymentMethods.SingleAsync(x => x.Code == "CARD", TestContext.Current.CancellationToken);
        var date = new DateOnly(2026, 9, 1);
        var paid = new RestaurantOrder { Id = Guid.NewGuid(), Branch = branch, OrderType = type, SalesChannelId = channel.Id, BusinessDate = date, Status = RestaurantOrderStatuses.Paid, Subtotal = 10, GrandTotal = 10 };
        paid.Items.Add(new() { Id = Guid.NewGuid(), MenuItem = item, MenuItemNameSnapshot = item.NameEn, Quantity = 2, UnitPriceSnapshot = 5, LineTotal = 10 });
        paid.Payments.Add(new() { Id = Guid.NewGuid(), PaymentMethod = cash, Amount = 4, CreatedAt = DateTime.UtcNow });
        paid.Payments.Add(new() { Id = Guid.NewGuid(), PaymentMethod = card, Amount = 6, CreatedAt = DateTime.UtcNow });
        var open = new RestaurantOrder { Id = Guid.NewGuid(), Branch = branch, OrderType = type, SalesChannelId = channel.Id, BusinessDate = date, Status = RestaurantOrderStatuses.Open, GrandTotal = 99 };
        var otherBranch = new Branch { Id = Guid.NewGuid(), Code = "B2", NameAr = "فرع آخر", NameEn = "Other branch" };
        var otherPaid = new RestaurantOrder { Id = Guid.NewGuid(), Branch = otherBranch, OrderType = type, SalesChannelId = channel.Id, BusinessDate = date, Status = RestaurantOrderStatuses.Paid, GrandTotal = 50 };
        db.AddRange(branch, otherBranch, category, item, channel, paid, open, otherPaid);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new ReportService(db, new User());

        var dashboard = await service.GetDashboardAsync(date, date, branch.Id, TestContext.Current.CancellationToken);
        var channels = await service.GetChannelDistributionAsync(date, date, branch.Id, TestContext.Current.CancellationToken);

        Assert.Equal(10, dashboard.TotalSales);
        Assert.Equal(1, dashboard.InvoiceCount);
        Assert.Equal(2, Assert.Single(dashboard.Products).QuantitySold);
        Assert.Collection(dashboard.PaymentBreakdown,
            payment => { Assert.Equal("CARD", payment.PaymentMethod); Assert.Equal(6, payment.TotalAmount); },
            payment => { Assert.Equal("CASH", payment.PaymentMethod); Assert.Equal(4, payment.TotalAmount); });
        Assert.Equal("TAKEAWAY", Assert.Single(dashboard.OrderTypes).Code);
        Assert.Equal(10, Assert.Single(channels).TotalSales);
    }

    private sealed class User : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid(); public Guid? BranchId => null; public string? RoleName => "GeneralManager";
        public IReadOnlyCollection<string> Permissions => [PermissionKeys.ReportsGlobalView]; public bool BypassBranchFilter => true;
    }
}
