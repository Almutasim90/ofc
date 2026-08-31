using Microsoft.EntityFrameworkCore;
using POS.Application.Common;
using POS.Application.Modifiers;
using POS.Application.RestaurantCatalog;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class ModifierTests
{
    [Fact]
    public async Task Required_single_and_optional_multiple_groups_are_enforced()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var catalog = new RestaurantCatalogService(db); var service = new ModifierService(db); var ct = TestContext.Current.CancellationToken;
        var category = await catalog.SaveCategoryAsync(null, new("برجر", "Burgers", 0), ct);
        var chicken = await catalog.SaveItemAsync(null, new(category.Id, "برجر دجاج", "Chicken Burger", MenuItemKinds.SingleProduct, 2, null, 0), ct);
        var heat = await service.SaveAsync(null, new("درجة الحار", "Heat level", 1, 1, true,
            [new(null, "عادي", "Regular", 0, true), new(null, "حار", "Spicy", .100m, true)], [chicken.Id]), ct);
        var extras = await service.SaveAsync(null, new("إضافات", "Extras", 0, 2, false,
            [new(null, "جبنة", "Cheese", .250m, true), new(null, "صوص", "Sauce", .100m, true)], [chicken.Id]), ct);

        await Assert.ThrowsAsync<ValidationException>(() => service.ValidateSelectionAsync(new(chicken.Id, []), ct));
        var regularOnly = await service.ValidateSelectionAsync(new(chicken.Id, [heat.Options[0].Id]), ct);
        Assert.Equal(0, regularOnly.PriceDelta);
        var customized = await service.ValidateSelectionAsync(new(chicken.Id, [heat.Options[1].Id, extras.Options[0].Id]), ct);
        Assert.Equal(.350m, customized.PriceDelta);
    }
}
