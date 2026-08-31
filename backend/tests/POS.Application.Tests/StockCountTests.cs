using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.RestaurantInventory;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class StockCountTests
{
    [Fact]
    public async Task Finalize_updates_stock_and_writes_adjustment_transaction()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        var branchId = Guid.NewGuid();
        var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Kilogram", Symbol = "kg", IsBase = true };
        var ingredient = new Ingredient { Id = Guid.NewGuid(), NameAr = "أرز", NameEn = "Rice", UnitOfMeasure = unit };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), BranchId = branchId, NameAr = "رئيسي", NameEn = "Main", IsActive = true };
        var stock = new WarehouseIngredientStock { Warehouse = warehouse, Ingredient = ingredient, CurrentQuantity = 10 };
        db.AddRange(unit, ingredient, warehouse, stock);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new StockCountService(db, new User(Guid.NewGuid()));

        var count = await service.Start(branchId, warehouse.Id, TestContext.Current.CancellationToken);
        Assert.Equal(10, Assert.Single(count.Lines).SystemQuantity);
        await service.Save(count.Id, [new CountLineInput(ingredient.Id, 7)], TestContext.Current.CancellationToken);
        await service.Finalize(count.Id, TestContext.Current.CancellationToken);

        Assert.Equal(7, stock.CurrentQuantity);
        Assert.Equal(StockCountStatuses.Finalized, db.StockCounts.Single().Status);
        Assert.Equal(-3, Assert.Single(db.RestaurantInventoryTransactions).QuantityChange);
    }

    private sealed class User(Guid id) : ICurrentUserService
    {
        public Guid? UserId => id;
        public Guid? BranchId => null;
        public string? RoleName => null;
        public IReadOnlyCollection<string> Permissions => [];
        public bool BypassBranchFilter => true;
    }
}
