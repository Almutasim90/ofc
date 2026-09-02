using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.RestaurantInventory;

public class RestaurantInventoryService(IAppDbContext db, ICurrentUserService user)
{
    public Task<List<UnitDto>> Units(CancellationToken ct = default) => db.UnitsOfMeasure.OrderBy(x => x.Name).Select(x => new UnitDto(x.Id, x.Name, x.Symbol, x.IsBase)).ToListAsync(ct);
    public Task<List<IngredientDto>> Ingredients(CancellationToken ct = default) => db.Ingredients.OrderBy(x => x.NameEn).Select(x => new IngredientDto(x.Id, x.NameAr, x.NameEn, x.UnitOfMeasureId)).ToListAsync(ct);
    public Task<List<WarehouseDto>> Warehouses(Guid branchId, CancellationToken ct = default) => db.Warehouses.Where(x => x.BranchId == branchId).Select(x => new WarehouseDto(x.Id, x.BranchId, x.NameAr, x.NameEn, x.IsDefault, x.IsActive)).ToListAsync(ct);
    public Task<List<ReasonDto>> Reasons(CancellationToken ct = default) => db.InventoryTransactionReasons.Where(x => x.IsActive).Select(x => new ReasonDto(x.Id, x.Code, x.NameAr, x.NameEn, x.IsActive)).ToListAsync(ct);
    public Task<List<StockDto>> Stock(Guid warehouseId, CancellationToken ct = default) => db.WarehouseIngredientStocks.Where(x => x.WarehouseId == warehouseId).Select(x => new StockDto(x.WarehouseId, x.IngredientId, x.Ingredient.NameEn, x.CurrentQuantity, x.LowStockThreshold)).ToListAsync(ct);

    public async Task<UnitDto> SaveUnit(string name, string symbol, bool isBase, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(symbol)) throw new ValidationException("Name and symbol are required.");
        var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = name.Trim(), Symbol = symbol.Trim(), IsBase = isBase };
        db.UnitsOfMeasure.Add(unit); await db.SaveChangesAsync(ct); return new(unit.Id, unit.Name, unit.Symbol, unit.IsBase);
    }

    public async Task<IngredientDto> SaveIngredient(string ar, string en, Guid unitId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ar) || string.IsNullOrWhiteSpace(en) || !await db.UnitsOfMeasure.AnyAsync(x => x.Id == unitId, ct)) throw new ValidationException("Valid names and unit are required.");
        var ingredient = new Ingredient { Id = Guid.NewGuid(), NameAr = ar.Trim(), NameEn = en.Trim(), UnitOfMeasureId = unitId };
        db.Ingredients.Add(ingredient); await db.SaveChangesAsync(ct); return new(ingredient.Id, ingredient.NameAr, ingredient.NameEn, ingredient.UnitOfMeasureId);
    }

    public async Task<WarehouseDto> SaveWarehouse(Guid branchId, string ar, string en, bool isDefault, CancellationToken ct = default)
    {
        if (isDefault) { var prior = await db.Warehouses.FirstOrDefaultAsync(x => x.BranchId == branchId && x.IsDefault, ct); if (prior is not null) prior.IsDefault = false; }
        var warehouse = new Warehouse { Id = Guid.NewGuid(), BranchId = branchId, NameAr = ar.Trim(), NameEn = en.Trim(), IsDefault = isDefault };
        db.Warehouses.Add(warehouse); await db.SaveChangesAsync(ct); return new(warehouse.Id, warehouse.BranchId, warehouse.NameAr, warehouse.NameEn, warehouse.IsDefault, warehouse.IsActive);
    }

    public async Task Move(StockMovementRequest request, CancellationToken ct = default)
    {
        if (request.Quantity == 0) throw new ValidationException("Quantity cannot be zero.");
        var reason = await db.InventoryTransactionReasons.FirstOrDefaultAsync(x => x.Id == request.ReasonId && x.IsActive, ct) ?? throw new ValidationException("Reason unavailable.");
        var stock = await db.WarehouseIngredientStocks.FirstOrDefaultAsync(x => x.WarehouseId == request.WarehouseId && x.IngredientId == request.IngredientId, ct);
        if (stock is null) { stock = new() { WarehouseId = request.WarehouseId, IngredientId = request.IngredientId }; db.WarehouseIngredientStocks.Add(stock); }
        if (stock.CurrentQuantity + request.Quantity < 0) throw new ValidationException("Insufficient stock.");
        stock.CurrentQuantity += request.Quantity;
        db.RestaurantInventoryTransactions.Add(new() { Id = Guid.NewGuid(), WarehouseId = request.WarehouseId, IngredientId = request.IngredientId, QuantityChange = request.Quantity, ReasonId = reason.Id, CreatedByUserId = RequireUser(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveRecipe(Guid menuItemId, Guid branchId, List<RecipeLineRequest> lines, CancellationToken ct = default)
    {
        if (lines.Any(x => x.QuantityRequired <= 0) || lines.Select(x => x.IngredientId).Distinct().Count() != lines.Count) throw new ValidationException("Recipe quantities must be positive and unique.");
        var old = await db.MenuItemRecipeLines.Where(x => x.MenuItemId == menuItemId && x.BranchId == branchId).ToListAsync(ct);
        db.MenuItemRecipeLines.RemoveRange(old);
        db.MenuItemRecipeLines.AddRange(lines.Select(x => new MenuItemRecipeLine { Id = Guid.NewGuid(), MenuItemId = menuItemId, BranchId = branchId, IngredientId = x.IngredientId, QuantityRequired = x.QuantityRequired }));
        await db.SaveChangesAsync(ct);
    }

    public Task Confirm(Guid orderId, CancellationToken ct = default) => Confirm(orderId, null, false, ct);

    public async Task Confirm(Guid orderId, Guid? capabilityBranchId, bool qrConfirmation, CancellationToken ct = default)
    {
        var orders = capabilityBranchId.HasValue ? db.RestaurantOrders.IgnoreQueryFilters() : db.RestaurantOrders;
        var order = await orders.Include(x => x.Items.Where(i => !i.IsCancelled)).ThenInclude(x => x.ComboSelections)
            .FirstOrDefaultAsync(x => x.Id == orderId && (!capabilityBranchId.HasValue || x.BranchId == capabilityBranchId), ct) ?? throw new NotFoundException("Order not found.");
        if (order.Status != RestaurantOrderStatuses.Open && (!qrConfirmation || order.Status is not (RestaurantOrderStatuses.PendingApproval or RestaurantOrderStatuses.Sent or RestaurantOrderStatuses.Paid))) throw new ValidationException("Only open or approved QR orders can be sent.");
        if (qrConfirmation && order.Status == RestaurantOrderStatuses.Sent && await db.RestaurantInventoryTransactions.IgnoreQueryFilters().AnyAsync(x => x.ReferenceOrderId == order.Id && x.QuantityChange < 0, ct)) return;
        var warehouses = capabilityBranchId.HasValue ? db.Warehouses.IgnoreQueryFilters() : db.Warehouses;
        var warehouse = await warehouses.FirstOrDefaultAsync(x => x.BranchId == order.BranchId && x.IsDefault && x.IsActive, ct) ?? throw new ValidationException("Default warehouse is required.");
        var itemIds = order.Items.Select(x => x.MenuItemId).Concat(order.Items.SelectMany(x => x.ComboSelections).Select(x => x.SelectedMenuItemId)).Distinct().ToList();
        var recipes = await db.MenuItemRecipeLines.IgnoreQueryFilters().Where(x => x.BranchId == order.BranchId && itemIds.Contains(x.MenuItemId)).ToListAsync(ct);
        var required = new Dictionary<Guid, decimal>();
        foreach (var line in order.Items)
            foreach (var id in new[] { line.MenuItemId }.Concat(line.ComboSelections.Select(x => x.SelectedMenuItemId)))
                foreach (var recipe in recipes.Where(x => x.MenuItemId == id)) required[recipe.IngredientId] = required.GetValueOrDefault(recipe.IngredientId) + recipe.QuantityRequired * line.Quantity;
        var stocks = await db.WarehouseIngredientStocks.IgnoreQueryFilters().Where(x => x.WarehouseId == warehouse.Id && required.Keys.Contains(x.IngredientId)).ToDictionaryAsync(x => x.IngredientId, ct);
        if (required.Any(x => !stocks.TryGetValue(x.Key, out var stock) || stock.CurrentQuantity < x.Value)) throw new ValidationException("Insufficient ingredient stock.");
        var reason = await db.InventoryTransactionReasons.SingleAsync(x => x.Code == "THEORETICAL_CONSUMPTION", ct);
        var actor = qrConfirmation ? user.UserId : RequireUser();
        foreach (var need in required)
        {
            stocks[need.Key].CurrentQuantity -= need.Value;
            db.RestaurantInventoryTransactions.Add(new() { Id = Guid.NewGuid(), WarehouseId = warehouse.Id, IngredientId = need.Key, QuantityChange = -need.Value, ReasonId = reason.Id, ReferenceOrderId = order.Id, CreatedByUserId = actor, CreatedAt = DateTime.UtcNow });
        }
        order.Status = RestaurantOrderStatuses.Sent;
        await db.SaveChangesAsync(ct);
    }

    public async Task StageReversal(Guid orderId, Guid? itemId, CancellationToken ct)
    {
        var reason = await db.InventoryTransactionReasons.SingleAsync(x => x.Code == "CANCELLATION_RETURN", ct);
        if (itemId is null)
        {
            var rows = await db.RestaurantInventoryTransactions.Where(x => x.ReferenceOrderId == orderId && x.QuantityChange < 0).ToListAsync(ct);
            foreach (var row in rows) await Return(row.WarehouseId, row.IngredientId, -row.QuantityChange, reason.Id, orderId, ct);
            return;
        }
        var order = await db.RestaurantOrders.Include(x => x.Items).ThenInclude(x => x.ComboSelections).SingleAsync(x => x.Id == orderId, ct);
        var item = order.Items.Single(x => x.Id == itemId);
        var warehouse = await db.Warehouses.SingleAsync(x => x.BranchId == order.BranchId && x.IsDefault && x.IsActive, ct);
        var ids = new[] { item.MenuItemId }.Concat(item.ComboSelections.Select(x => x.SelectedMenuItemId)).ToList();
        var recipes = await db.MenuItemRecipeLines.Where(x => x.BranchId == order.BranchId && ids.Contains(x.MenuItemId)).ToListAsync(ct);
        foreach (var group in recipes.GroupBy(x => x.IngredientId)) await Return(warehouse.Id, group.Key, group.Sum(x => x.QuantityRequired) * item.Quantity, reason.Id, orderId, ct);
    }

    private async Task Return(Guid warehouseId, Guid ingredientId, decimal quantity, Guid reasonId, Guid orderId, CancellationToken ct)
    {
        var stock = await db.WarehouseIngredientStocks.SingleAsync(x => x.WarehouseId == warehouseId && x.IngredientId == ingredientId, ct);
        stock.CurrentQuantity += quantity;
        db.RestaurantInventoryTransactions.Add(new() { Id = Guid.NewGuid(), WarehouseId = warehouseId, IngredientId = ingredientId, QuantityChange = quantity, ReasonId = reasonId, ReferenceOrderId = orderId, CreatedByUserId = RequireUser(), CreatedAt = DateTime.UtcNow });
    }

    private Guid RequireUser() => user.UserId ?? throw new ValidationException("Authenticated user required.");
}
