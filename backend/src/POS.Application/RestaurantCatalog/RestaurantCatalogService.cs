using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.RestaurantCatalog;

public class RestaurantCatalogService(IAppDbContext db)
{
    public Task<List<RestaurantTableDto>> GetTablesAsync(Guid branchId, CancellationToken ct = default) =>
        db.RestaurantTables.Where(x => x.BranchId == branchId).OrderBy(x => x.Label)
            .Select(x => new RestaurantTableDto(x.Id, x.BranchId, x.Label, x.Capacity, x.IsActive)).ToListAsync(ct);

    public async Task<RestaurantTableDto> SaveTableAsync(Guid? id, SaveRestaurantTableRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Label)) throw new ValidationException("Table label is required.");
        if (request.Capacity is <= 0) throw new ValidationException("Capacity must be greater than zero.");
        if (!await db.Branches.AnyAsync(x => x.Id == request.BranchId, ct)) throw new NotFoundException("Branch not found.");
        var label = request.Label.Trim();
        if (await db.RestaurantTables.AnyAsync(x => x.BranchId == request.BranchId && x.Label == label && x.Id != id, ct))
            throw new ValidationException("Table label already exists in this branch.");
        var row = id is null ? new RestaurantTable { Id = Guid.NewGuid() } :
            await db.RestaurantTables.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Table not found.");
        if (id is null) db.RestaurantTables.Add(row);
        row.BranchId = request.BranchId; row.Label = label; row.Capacity = request.Capacity; row.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new(row.Id, row.BranchId, row.Label, row.Capacity, row.IsActive);
    }

    public Task<List<BranchFeatureFlagDto>> GetFlagsAsync(Guid branchId, CancellationToken ct = default) =>
        db.BranchFeatureFlags.Where(x => x.BranchId == branchId).OrderBy(x => x.FeatureKey)
            .Select(x => new BranchFeatureFlagDto(x.Id, x.BranchId, x.FeatureKey, x.IsEnabled)).ToListAsync(ct);

    public async Task<BranchFeatureFlagDto> SetFlagAsync(Guid branchId, string key, bool enabled, CancellationToken ct = default)
    {
        if (!await db.Branches.AnyAsync(x => x.Id == branchId, ct)) throw new NotFoundException("Branch not found.");
        var normalized = key.Trim().ToUpperInvariant();
        if (normalized.Length is < 2 or > 100 || normalized.Any(x => !(char.IsAsciiLetterUpper(x) || char.IsDigit(x) || x == '_')))
            throw new ValidationException("Feature key must use letters, numbers, and underscores only.");
        var row = await db.BranchFeatureFlags.FirstOrDefaultAsync(x => x.BranchId == branchId && x.FeatureKey == normalized, ct);
        if (row is null) { row = new() { Id = Guid.NewGuid(), BranchId = branchId, FeatureKey = normalized }; db.BranchFeatureFlags.Add(row); }
        row.IsEnabled = enabled;
        await db.SaveChangesAsync(ct);
        return new(row.Id, row.BranchId, row.FeatureKey, row.IsEnabled);
    }

    public async Task<List<MenuCategoryDto>> GetCategoriesAsync(Guid? branchId, CancellationToken ct = default)
    {
        var query = db.MenuCategories.AsNoTracking();
        return await query.OrderBy(x => x.SortOrder).ThenBy(x => x.NameEn).Select(x => new MenuCategoryDto(
            x.Id, x.NameAr, x.NameEn, x.SortOrder, x.IsActive,
            branchId == null || !db.CategoryBranchAvailabilities.Any(a => a.CategoryId == x.Id && a.BranchId == branchId && !a.IsAvailable)))
            .ToListAsync(ct);
    }

    public async Task<MenuCategoryDto> SaveCategoryAsync(Guid? id, SaveMenuCategoryRequest request, CancellationToken ct = default)
    {
        ValidateNames(request.NameAr, request.NameEn); if (request.SortOrder < 0) throw new ValidationException("Sort order cannot be negative.");
        var row = id is null ? new MenuCategory { Id = Guid.NewGuid() } :
            await db.MenuCategories.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Category not found.");
        if (id is null) db.MenuCategories.Add(row);
        row.NameAr = request.NameAr.Trim(); row.NameEn = request.NameEn.Trim(); row.SortOrder = request.SortOrder; row.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new(row.Id, row.NameAr, row.NameEn, row.SortOrder, row.IsActive, true);
    }

    public async Task ReorderCategoriesAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count != ids.Distinct().Count()) throw new ValidationException("Category order contains duplicates.");
        var rows = await db.MenuCategories.Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        if (rows.Count != ids.Count) throw new ValidationException("One or more categories were not found.");
        for (var i = 0; i < ids.Count; i++) rows.Single(x => x.Id == ids[i]).SortOrder = i;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetCategoryAvailabilityAsync(Guid categoryId, Guid branchId, bool available, CancellationToken ct = default)
    {
        if (!await db.MenuCategories.AnyAsync(x => x.Id == categoryId, ct) || !await db.Branches.AnyAsync(x => x.Id == branchId, ct))
            throw new NotFoundException("Category or branch not found.");
        var row = await db.CategoryBranchAvailabilities.FirstOrDefaultAsync(x => x.CategoryId == categoryId && x.BranchId == branchId, ct);
        if (row is null) { row = new() { Id = Guid.NewGuid(), CategoryId = categoryId, BranchId = branchId }; db.CategoryBranchAvailabilities.Add(row); }
        row.IsAvailable = available; await db.SaveChangesAsync(ct);
    }

    public Task<List<MenuItemDto>> GetItemsAsync(Guid? categoryId, CancellationToken ct = default) =>
        db.MenuItems.Where(x => categoryId == null || x.CategoryId == categoryId).OrderBy(x => x.Category.SortOrder).ThenBy(x => x.SortOrder)
            .Select(x => new MenuItemDto(x.Id, x.CategoryId, x.NameAr, x.NameEn, x.Kind, x.BasePrice, x.ImageUrl, x.SortOrder, x.IsActive)).ToListAsync(ct);

    public async Task<MenuItemDto> SaveItemAsync(Guid? id, SaveMenuItemRequest request, CancellationToken ct = default)
    {
        ValidateNames(request.NameAr, request.NameEn);
        if (!MenuItemKinds.All.Contains(request.Kind)) throw new ValidationException("Invalid menu item kind.");
        if (request.BasePrice < 0 || request.SortOrder < 0) throw new ValidationException("Price and sort order cannot be negative.");
        if (!await db.MenuCategories.AnyAsync(x => x.Id == request.CategoryId, ct)) throw new NotFoundException("Category not found.");
        var row = id is null ? new MenuItem { Id = Guid.NewGuid() } :
            await db.MenuItems.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Menu item not found.");
        if (id is null) db.MenuItems.Add(row);
        row.CategoryId = request.CategoryId; row.NameAr = request.NameAr.Trim(); row.NameEn = request.NameEn.Trim(); row.Kind = request.Kind;
        row.BasePrice = request.BasePrice; row.ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : request.ImageUrl.Trim(); row.SortOrder = request.SortOrder; row.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return ToDto(row);
    }

    public async Task<List<ComboComponentDto>> GetComboAsync(Guid comboId, CancellationToken ct = default) =>
        await db.ComboComponents.Where(x => x.ComboMenuItemId == comboId).OrderBy(x => x.SortOrder)
            .Select(x => new ComboComponentDto(x.Id, x.SlotLabel, x.IsRequired, x.MinSelect, x.MaxSelect, x.SortOrder,
                x.Options.OrderByDescending(o => o.IsDefault).ThenBy(o => o.MenuItem.NameEn)
                    .Select(o => new ComboOptionDto(o.Id, o.MenuItemId, o.MenuItem.NameAr, o.MenuItem.NameEn, o.PriceDelta, o.IsDefault)).ToList()))
            .ToListAsync(ct);

    public async Task SaveComboAsync(Guid comboId, SaveComboDefinitionRequest request, CancellationToken ct = default)
    {
        var combo = await db.MenuItems.FirstOrDefaultAsync(x => x.Id == comboId, ct) ?? throw new NotFoundException("Combo not found.");
        if (combo.Kind != MenuItemKinds.Combo) throw new ValidationException("Only combo menu items can have components.");
        var optionIds = request.Components.SelectMany(x => x.Options).Select(x => x.MenuItemId).Distinct().ToList();
        var validOptionCount = await db.MenuItems.CountAsync(x => optionIds.Contains(x.Id) && x.Kind == MenuItemKinds.SingleProduct && x.IsActive, ct);
        if (validOptionCount != optionIds.Count) throw new ValidationException("All combo options must be active single products.");
        foreach (var component in request.Components)
        {
            if (string.IsNullOrWhiteSpace(component.SlotLabel) || component.MinSelect < 0 || component.MaxSelect < component.MinSelect || component.Options.Count == 0)
                throw new ValidationException("Each combo slot needs a label, valid selection limits, and at least one option.");
            if (component.Options.Count(x => x.IsDefault) > 1) throw new ValidationException("Each combo slot can have only one default option.");
            if (component.Options.Select(x => x.MenuItemId).Distinct().Count() != component.Options.Count) throw new ValidationException("A combo slot cannot contain duplicate options.");
        }
        var existing = await db.ComboComponents.Where(x => x.ComboMenuItemId == comboId).ToListAsync(ct);
        db.ComboComponents.RemoveRange(existing);
        foreach (var input in request.Components)
        {
            var component = new ComboComponent { Id = Guid.NewGuid(), ComboMenuItemId = comboId, SlotLabel = input.SlotLabel.Trim(), IsRequired = input.IsRequired, MinSelect = input.MinSelect, MaxSelect = input.MaxSelect, SortOrder = input.SortOrder };
            component.Options = input.Options.Select(x => new ComboComponentOption { Id = Guid.NewGuid(), MenuItemId = x.MenuItemId, PriceDelta = x.PriceDelta, IsDefault = x.IsDefault }).ToList();
            db.ComboComponents.Add(component);
        }
        await db.SaveChangesAsync(ct);
    }

    private static void ValidateNames(string ar, string en) { if (string.IsNullOrWhiteSpace(ar) || string.IsNullOrWhiteSpace(en)) throw new ValidationException("Arabic and English names are required."); }
    private static MenuItemDto ToDto(MenuItem x) => new(x.Id, x.CategoryId, x.NameAr, x.NameEn, x.Kind, x.BasePrice, x.ImageUrl, x.SortOrder, x.IsActive);
}
