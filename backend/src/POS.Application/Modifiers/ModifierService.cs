using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Modifiers;

public class ModifierService(IAppDbContext db)
{
    public Task<List<ModifierGroupDto>> GetAsync(Guid? menuItemId = null, CancellationToken ct = default) =>
        db.ModifierGroups.Where(x => menuItemId == null || x.MenuItems.Any(m => m.MenuItemId == menuItemId))
            .OrderBy(x => x.NameEn).Select(x => new ModifierGroupDto(x.Id, x.NameAr, x.NameEn, x.MinSelect, x.MaxSelect, x.IsRequired,
                x.Options.OrderBy(o => o.NameEn).Select(o => new ModifierOptionDto(o.Id, o.NameAr, o.NameEn, o.PriceDelta, o.IsActive)).ToList(),
                x.MenuItems.Select(m => m.MenuItemId).ToList())).ToListAsync(ct);

    public async Task<ModifierGroupDto> SaveAsync(Guid? id, SaveModifierGroupRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var itemIds = request.MenuItemIds.Distinct().ToList();
        if (await db.MenuItems.CountAsync(x => itemIds.Contains(x.Id) && x.Kind == MenuItemKinds.SingleProduct, ct) != itemIds.Count)
            throw new ValidationException("Modifier groups can only be linked to single products.");
        var row = id is null ? new ModifierGroup { Id = Guid.NewGuid() } :
            await db.ModifierGroups.Include(x => x.Options).Include(x => x.MenuItems).FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("Modifier group not found.");
        if (id is null) db.ModifierGroups.Add(row);
        row.NameAr = request.NameAr.Trim(); row.NameEn = request.NameEn.Trim(); row.MinSelect = request.MinSelect;
        row.MaxSelect = request.MaxSelect; row.IsRequired = request.IsRequired;
        if (id is not null) { db.ModifierOptions.RemoveRange(row.Options); db.MenuItemModifierGroups.RemoveRange(row.MenuItems); }
        row.Options = request.Options.Select(x => new ModifierOption { Id = Guid.NewGuid(), NameAr = x.NameAr.Trim(), NameEn = x.NameEn.Trim(), PriceDelta = x.PriceDelta, IsActive = x.IsActive }).ToList();
        row.MenuItems = itemIds.Select(x => new MenuItemModifierGroup { MenuItemId = x }).ToList();
        await db.SaveChangesAsync(ct);
        return (await GetAsync(null, ct)).Single(x => x.Id == row.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.ModifierGroups.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Modifier group not found.");
        db.ModifierGroups.Remove(row); await db.SaveChangesAsync(ct);
    }

    public async Task<ValidatedModifierSelectionDto> ValidateSelectionAsync(ValidateModifierSelectionRequest request, CancellationToken ct = default)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(x => x.Id == request.MenuItemId && x.Kind == MenuItemKinds.SingleProduct && x.IsActive, ct)
            ?? throw new NotFoundException("Active single product not found.");
        var groups = await db.ModifierGroups.Where(x => x.MenuItems.Any(m => m.MenuItemId == item.Id)).Include(x => x.Options).ToListAsync(ct);
        var selectedIds = request.ModifierOptionIds.Distinct().ToList();
        if (selectedIds.Count != request.ModifierOptionIds.Count) throw new ValidationException("Modifier option cannot be selected more than once.");
        var allowed = groups.SelectMany(x => x.Options).Where(x => x.IsActive).ToDictionary(x => x.Id);
        if (selectedIds.Any(x => !allowed.ContainsKey(x))) throw new ValidationException("One or more modifier options are unavailable for this product.");
        foreach (var group in groups)
        {
            var count = group.Options.Count(x => selectedIds.Contains(x.Id));
            var minimum = group.IsRequired ? Math.Max(1, group.MinSelect) : group.MinSelect;
            if (count < minimum || count > group.MaxSelect) throw new ValidationException($"Selection for '{group.NameEn}' must be between {minimum} and {group.MaxSelect}.");
        }
        var selected = selectedIds.Select(x => allowed[x]).ToList();
        return new(selected.Sum(x => x.PriceDelta), selected.Select(x => new ModifierOptionDto(x.Id, x.NameAr, x.NameEn, x.PriceDelta, x.IsActive)).ToList());
    }

    private static void Validate(SaveModifierGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NameAr) || string.IsNullOrWhiteSpace(request.NameEn)) throw new ValidationException("Arabic and English names are required.");
        if (request.MinSelect < 0 || request.MaxSelect < request.MinSelect || request.MaxSelect < 1) throw new ValidationException("Invalid selection limits.");
        if (request.IsRequired && request.MaxSelect < 1) throw new ValidationException("Required groups need at least one selection.");
        if (request.Options.Count == 0 || request.Options.Any(x => string.IsNullOrWhiteSpace(x.NameAr) || string.IsNullOrWhiteSpace(x.NameEn))) throw new ValidationException("At least one named option is required.");
        if (request.MaxSelect > request.Options.Count(x => x.IsActive)) throw new ValidationException("Maximum selections cannot exceed active options.");
        if (request.Options.Select(x => x.NameEn.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Options.Count) throw new ValidationException("Option names must be unique within a group.");
    }
}
