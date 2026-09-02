using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.RestaurantCatalog;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class RestaurantCatalogTests
{
    [Fact]
    public async Task Ordinary_categories_are_dynamic_and_branch_availability_is_fail_open()
    {
        await using var db = CreateDb(); var branch = Branch(); db.Branches.Add(branch); await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new RestaurantCatalogService(db);
        var offers = await service.SaveCategoryAsync(null, new("العروض", "Offers", 0), TestContext.Current.CancellationToken);
        var kids = await service.SaveCategoryAsync(null, new("وجبات الأطفال", "Kids Meal", 1), TestContext.Current.CancellationToken);

        var initial = await service.GetCategoriesAsync(branch.Id, TestContext.Current.CancellationToken);
        Assert.All(initial, x => Assert.True(x.IsAvailable));
        await service.SetCategoryAvailabilityAsync(offers.Id, branch.Id, false, TestContext.Current.CancellationToken);
        var configured = await service.GetCategoriesAsync(branch.Id, TestContext.Current.CancellationToken);
        Assert.False(configured.Single(x => x.Id == offers.Id).IsAvailable);
        Assert.True(configured.Single(x => x.Id == kids.Id).IsAvailable);
    }

    [Fact]
    public async Task Combo_supports_three_slots_options_and_price_deltas()
    {
        await using var db = CreateDb(); var service = new RestaurantCatalogService(db);
        var category = await service.SaveCategoryAsync(null, new("الوجبات", "Meals", 0), TestContext.Current.CancellationToken);
        async Task<MenuItemDto> Single(string ar, string en) => await service.SaveItemAsync(null, new(category.Id, ar, en, MenuItemKinds.SingleProduct, 1, null, 0), TestContext.Current.CancellationToken);
        var burger = await Single("برجر", "Burger"); var fries = await Single("بطاطس", "Fries"); var drink = await Single("مشروب", "Drink");
        var combo = await service.SaveItemAsync(null, new(category.Id, "وجبة برجر", "Burger Meal", MenuItemKinds.Combo, 3, null, 1), TestContext.Current.CancellationToken);
        var components = new[]
        {
            Slot("الصنف الرئيسي", burger.Id, 0), Slot("الجانبي", fries.Id, .500m), Slot("المشروب", drink.Id, .250m),
        };
        await service.SaveComboAsync(combo.Id, new(components), TestContext.Current.CancellationToken);
        var saved = await service.GetComboAsync(combo.Id, TestContext.Current.CancellationToken);
        Assert.Equal(3, saved.Count); Assert.Equal(.500m, saved[1].Options.Single().PriceDelta); Assert.All(saved, x => Assert.True(x.Options.Single().IsDefault));
    }

    [Fact]
    public async Task Combo_rejects_another_combo_as_an_option()
    {
        await using var db = CreateDb(); var service = new RestaurantCatalogService(db);
        var category = await service.SaveCategoryAsync(null, new("الوجبات", "Meals", 0), TestContext.Current.CancellationToken);
        var combo = await service.SaveItemAsync(null, new(category.Id, "وجبة", "Meal", MenuItemKinds.Combo, 3, null, 0), TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ValidationException>(() => service.SaveComboAsync(combo.Id, new([Slot("خيار", combo.Id, 0)]), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Combo_rejects_selection_limits_above_available_options()
    {
        await using var db = CreateDb(); var service = new RestaurantCatalogService(db);
        var category = await service.SaveCategoryAsync(null, new("الوجبات", "Meals", 0), TestContext.Current.CancellationToken);
        var option = await service.SaveItemAsync(null, new(category.Id, "خيار", "Option", MenuItemKinds.SingleProduct, 1, null, 0), TestContext.Current.CancellationToken);
        var combo = await service.SaveItemAsync(null, new(category.Id, "وجبة", "Meal", MenuItemKinds.Combo, 3, null, 1), TestContext.Current.CancellationToken);
        var slot = new SaveComboComponentRequest("Main", true, 1, 2, 0, [new(option.Id, 0, true)]);

        await Assert.ThrowsAsync<ValidationException>(() => service.SaveComboAsync(combo.Id, new([slot]), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Combo_slot_with_a_minimum_requires_a_default_option()
    {
        await using var db = CreateDb(); var service = new RestaurantCatalogService(db);
        var category = await service.SaveCategoryAsync(null, new("الوجبات", "Meals", 0), TestContext.Current.CancellationToken);
        var option = await service.SaveItemAsync(null, new(category.Id, "خيار", "Option", MenuItemKinds.SingleProduct, 1, null, 0), TestContext.Current.CancellationToken);
        var combo = await service.SaveItemAsync(null, new(category.Id, "وجبة", "Meal", MenuItemKinds.Combo, 3, null, 1), TestContext.Current.CancellationToken);
        var slot = new SaveComboComponentRequest("Main", true, 1, 1, 0, [new(option.Id, 0, false)]);

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.SaveComboAsync(combo.Id, new([slot]), TestContext.Current.CancellationToken));
        Assert.Contains("default", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SaveComboComponentRequest Slot(string label, Guid itemId, decimal delta) => new(label, true, 1, 1, 0, [new(itemId, delta, true)]);
    private static Branch Branch() => new() { Id = Guid.NewGuid(), NameAr = "فرع", NameEn = "Branch", Code = Guid.NewGuid().ToString("N") };
    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
