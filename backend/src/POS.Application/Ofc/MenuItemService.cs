using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Application.Ofc;

public class MenuItemService(IAppDbContext db)
{
    public async Task<List<MenuItemDto>> GetAllAsync(Guid? categoryId, CancellationToken cancellationToken = default)
    {
        var query = db.MenuItems.AsQueryable();
        if (categoryId is not null) query = query.Where(i => i.CategoryId == categoryId);

        return await query
            .OrderBy(i => i.SortOrder)
            .Select(i => new MenuItemDto(i.Id, i.CategoryId, i.NameAr, i.NameEn, i.Kind, i.BasePrice, i.ImageUrl, i.SortOrder, i.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<MenuItemDto> CreateAsync(CreateMenuItemRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Kind is not (MenuItemKind.SingleProduct or MenuItemKind.Combo))
            throw new ValidationException($"Unknown menu item kind '{request.Kind}'.");
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
            throw new NotFoundException($"Category '{request.CategoryId}' not found.");

        var item = new MenuItem
        {
            Id = Guid.NewGuid(),
            CategoryId = request.CategoryId,
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Kind = request.Kind,
            BasePrice = request.BasePrice,
            ImageUrl = request.ImageUrl,
            SortOrder = request.SortOrder,
            IsActive = true,
        };

        db.MenuItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);

        return new MenuItemDto(item.Id, item.CategoryId, item.NameAr, item.NameEn, item.Kind, item.BasePrice, item.ImageUrl, item.SortOrder, item.IsActive);
    }

    public async Task<MenuItemDto> UpdateAsync(Guid id, UpdateMenuItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Menu item '{id}' not found.");
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
            throw new NotFoundException($"Category '{request.CategoryId}' not found.");

        item.CategoryId = request.CategoryId;
        item.NameAr = request.NameAr;
        item.NameEn = request.NameEn;
        item.BasePrice = request.BasePrice;
        item.ImageUrl = request.ImageUrl;
        item.SortOrder = request.SortOrder;
        item.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return new MenuItemDto(item.Id, item.CategoryId, item.NameAr, item.NameEn, item.Kind, item.BasePrice, item.ImageUrl, item.SortOrder, item.IsActive);
    }

    public async Task<List<ComboComponentDto>> GetComboComponentsAsync(Guid comboMenuItemId, CancellationToken cancellationToken = default)
    {
        await EnsureComboAsync(comboMenuItemId, cancellationToken);

        return await db.ComboComponents
            .Where(c => c.ComboMenuItemId == comboMenuItemId)
            .OrderBy(c => c.SlotLabel)
            .Select(c => new ComboComponentDto(
                c.SlotLabel, c.IsRequired, c.MinSelect, c.MaxSelect,
                c.Options.Select(o => new ComboComponentOptionDto(
                    o.MenuItemId, o.MenuItem.NameAr, o.MenuItem.NameEn, o.PriceDelta, o.IsDefault)).ToList()))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Replaces every slot and option in one go - same "remove all, re-add" pattern as
    /// RecipeService.SetAsync, since a combo's component list is always edited as a whole.</summary>
    public async Task SetComboComponentsAsync(Guid comboMenuItemId, SetComboComponentsRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureComboAsync(comboMenuItemId, cancellationToken);

        var existing = await db.ComboComponents
            .Where(c => c.ComboMenuItemId == comboMenuItemId)
            .ToListAsync(cancellationToken);
        db.ComboComponents.RemoveRange(existing);

        foreach (var componentInput in request.Components)
        {
            if (componentInput.MinSelect < 0 || componentInput.MaxSelect < componentInput.MinSelect)
                throw new ValidationException($"Invalid select range for slot '{componentInput.SlotLabel}'.");

            var component = new ComboComponent
            {
                Id = Guid.NewGuid(),
                ComboMenuItemId = comboMenuItemId,
                SlotLabel = componentInput.SlotLabel,
                IsRequired = componentInput.IsRequired,
                MinSelect = componentInput.MinSelect,
                MaxSelect = componentInput.MaxSelect,
            };

            foreach (var optionInput in componentInput.Options)
            {
                component.Options.Add(new ComboComponentOption
                {
                    Id = Guid.NewGuid(),
                    MenuItemId = optionInput.MenuItemId,
                    PriceDelta = optionInput.PriceDelta,
                    IsDefault = optionInput.IsDefault,
                });
            }

            db.ComboComponents.Add(component);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureComboAsync(Guid comboMenuItemId, CancellationToken cancellationToken)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(i => i.Id == comboMenuItemId, cancellationToken)
            ?? throw new NotFoundException($"Menu item '{comboMenuItemId}' not found.");
        if (item.Kind != MenuItemKind.Combo)
            throw new ValidationException($"Menu item '{comboMenuItemId}' is not a combo.");
    }
}
