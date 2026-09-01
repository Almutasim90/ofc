using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
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
        var transaction = Assert.Single(db.RestaurantInventoryTransactions.Include(x => x.Reason));
        Assert.Equal(-3, transaction.QuantityChange);
        Assert.Equal("INVENTORY_COUNT_ADJUSTMENT", transaction.Reason.Code);
    }

    [Fact]
    public async Task Start_includes_zero_stock_ingredients_and_prevents_competing_drafts()
    {
        await using var db = CreateDb();
        var (branchId, warehouse, ingredient) = await SeedInventory(db, null);
        var service = new StockCountService(db, new User(Guid.NewGuid()));

        var count = await service.Start(branchId, warehouse.Id, TestContext.Current.CancellationToken);

        var line = Assert.Single(count.Lines);
        Assert.Equal(ingredient.Id, line.IngredientId);
        Assert.Equal(0, line.SystemQuantity);
        Assert.Equal(count.Id, (await service.GetDraft(warehouse.Id, TestContext.Current.CancellationToken))?.Id);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.Start(branchId, warehouse.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Save_rejects_duplicate_or_unknown_lines()
    {
        await using var db = CreateDb();
        var (branchId, warehouse, ingredient) = await SeedInventory(db, 10);
        var service = new StockCountService(db, new User(Guid.NewGuid()));
        var count = await service.Start(branchId, warehouse.Id, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ValidationException>(() => service.Save(count.Id,
            [new(ingredient.Id, 8), new(ingredient.Id, 8)], TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ValidationException>(() => service.Save(count.Id,
            [new(Guid.NewGuid(), 8)], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Finalize_rejects_stock_changed_after_count_started()
    {
        await using var db = CreateDb();
        var (branchId, warehouse, ingredient) = await SeedInventory(db, 10);
        var service = new StockCountService(db, new User(Guid.NewGuid()));
        var count = await service.Start(branchId, warehouse.Id, TestContext.Current.CancellationToken);
        await service.Save(count.Id, [new(ingredient.Id, 7)], TestContext.Current.CancellationToken);
        db.WarehouseIngredientStocks.Single().CurrentQuantity = 9;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ConflictException>(() => service.Finalize(count.Id, TestContext.Current.CancellationToken));

        Assert.Equal(StockCountStatuses.Draft, db.StockCounts.Single().Status);
        Assert.Empty(db.RestaurantInventoryTransactions);
    }

    [Fact]
    public async Task Finalize_creates_stock_discovered_during_count()
    {
        await using var db = CreateDb();
        var (branchId, warehouse, ingredient) = await SeedInventory(db, null);
        var service = new StockCountService(db, new User(Guid.NewGuid()));
        var count = await service.Start(branchId, warehouse.Id, TestContext.Current.CancellationToken);
        await service.Save(count.Id, [new(ingredient.Id, 2)], TestContext.Current.CancellationToken);

        await service.Finalize(count.Id, TestContext.Current.CancellationToken);

        Assert.Equal(2, Assert.Single(db.WarehouseIngredientStocks).CurrentQuantity);
        Assert.Equal(2, Assert.Single(db.RestaurantInventoryTransactions).QuantityChange);
    }

    private static AppDbContext CreateDb()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<(Guid BranchId, Warehouse Warehouse, Ingredient Ingredient)> SeedInventory(
        AppDbContext db,
        decimal? quantity)
    {
        var branchId = Guid.NewGuid();
        var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = $"Kilogram-{Guid.NewGuid()}", Symbol = "kg", IsBase = true };
        var ingredient = new Ingredient { Id = Guid.NewGuid(), NameAr = "أرز", NameEn = "Rice", UnitOfMeasure = unit };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), BranchId = branchId, NameAr = "رئيسي", NameEn = "Main", IsActive = true };
        db.AddRange(unit, ingredient, warehouse);
        if (quantity.HasValue)
            db.Add(new WarehouseIngredientStock { Warehouse = warehouse, Ingredient = ingredient, CurrentQuantity = quantity.Value });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (branchId, warehouse, ingredient);
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
