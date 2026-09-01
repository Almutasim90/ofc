using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.RestaurantInventory;

public record StockCountLineDto(
    Guid IngredientId,
    string NameAr,
    string NameEn,
    decimal SystemQuantity,
    decimal CountedQuantity,
    decimal VarianceQuantity);

public record StockCountDto(
    Guid Id,
    Guid BranchId,
    Guid WarehouseId,
    DateTime CreatedAt,
    string Status,
    List<StockCountLineDto> Lines);

public record CountLineInput(Guid IngredientId, decimal CountedQuantity);

public class StockCountService(IAppDbContext db, ICurrentUserService user)
{
    public async Task<StockCountDto> Start(Guid branchId, Guid warehouseId, CancellationToken ct)
    {
        if (!await db.Warehouses.AnyAsync(x => x.Id == warehouseId && x.BranchId == branchId && x.IsActive, ct))
            throw new ValidationException("Warehouse unavailable.");

        if (await db.StockCounts.AnyAsync(x => x.WarehouseId == warehouseId && x.Status == StockCountStatuses.Draft, ct))
            throw new ConflictException("This warehouse already has a draft count.");

        var ingredients = await db.Ingredients.OrderBy(x => x.NameEn).ToListAsync(ct);
        var stocks = await db.WarehouseIngredientStocks
            .Where(x => x.WarehouseId == warehouseId)
            .ToDictionaryAsync(x => x.IngredientId, ct);

        var count = new StockCount
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            WarehouseId = warehouseId,
            CountedByUserId = UserId(),
            CreatedAt = DateTime.UtcNow,
            Lines = ingredients.Select(ingredient =>
            {
                var quantity = stocks.TryGetValue(ingredient.Id, out var stock) ? stock.CurrentQuantity : 0;
                return new StockCountLine
                {
                    IngredientId = ingredient.Id,
                    SystemQuantity = quantity,
                    CountedQuantity = quantity,
                };
            }).ToList(),
        };

        db.StockCounts.Add(count);
        await db.SaveChangesAsync(ct);
        return await Get(count.Id, ct);
    }

    public async Task<StockCountDto?> GetDraft(Guid warehouseId, CancellationToken ct)
    {
        return await Project(db.StockCounts.Where(x =>
                x.WarehouseId == warehouseId && x.Status == StockCountStatuses.Draft))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<StockCountDto> Get(Guid id, CancellationToken ct)
    {
        return await Project(db.StockCounts.Where(x => x.Id == id)).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Count not found.");
    }

    public async Task Save(Guid id, List<CountLineInput>? input, CancellationToken ct)
    {
        var count = await db.StockCounts.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Count not found.");
        if (count.Status != StockCountStatuses.Draft)
            throw new ValidationException("Finalized count cannot be changed.");

        ValidateLines(count.Lines, input);
        var rows = input!.ToDictionary(x => x.IngredientId);
        foreach (var line in count.Lines)
        {
            line.CountedQuantity = rows[line.IngredientId].CountedQuantity;
            line.VarianceQuantity = line.CountedQuantity - line.SystemQuantity;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task Finalize(Guid id, CancellationToken ct)
    {
        var count = await db.StockCounts.Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Count not found.");
        if (count.Status != StockCountStatuses.Draft)
            throw new ValidationException("Count is already finalized.");

        var stocks = await db.WarehouseIngredientStocks
            .Where(x => x.WarehouseId == count.WarehouseId)
            .ToDictionaryAsync(x => x.IngredientId, ct);

        if (count.Lines.Any(line =>
                (stocks.TryGetValue(line.IngredientId, out var stock) ? stock.CurrentQuantity : 0) != line.SystemQuantity))
            throw new ConflictException("Warehouse stock changed after this count started. Start a new count.");

        var reasonId = await db.InventoryTransactionReasons
            .Where(x => x.Code == "INVENTORY_COUNT_ADJUSTMENT")
            .Select(x => x.Id)
            .SingleAsync(ct);
        var userId = UserId();
        var now = DateTime.UtcNow;

        foreach (var line in count.Lines.Where(x => x.VarianceQuantity != 0))
        {
            if (!stocks.TryGetValue(line.IngredientId, out var stock))
            {
                stock = new WarehouseIngredientStock
                {
                    WarehouseId = count.WarehouseId,
                    IngredientId = line.IngredientId,
                };
                db.WarehouseIngredientStocks.Add(stock);
            }

            stock.CurrentQuantity = line.CountedQuantity;
            db.RestaurantInventoryTransactions.Add(new RestaurantInventoryTransaction
            {
                Id = Guid.NewGuid(),
                WarehouseId = count.WarehouseId,
                IngredientId = line.IngredientId,
                QuantityChange = line.VarianceQuantity,
                ReasonId = reasonId,
                CreatedByUserId = userId,
                CreatedAt = now,
            });
        }

        count.Status = StockCountStatuses.Finalized;
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<StockCountDto> Project(IQueryable<StockCount> query) => query.Select(count => new StockCountDto(
        count.Id,
        count.BranchId,
        count.WarehouseId,
        count.CreatedAt,
        count.Status,
        count.Lines.OrderBy(line => line.Ingredient.NameEn).Select(line => new StockCountLineDto(
            line.IngredientId,
            line.Ingredient.NameAr,
            line.Ingredient.NameEn,
            line.SystemQuantity,
            line.CountedQuantity,
            line.VarianceQuantity)).ToList()));

    private static void ValidateLines(ICollection<StockCountLine> lines, List<CountLineInput>? input)
    {
        if (input is null || input.Any(x => x.CountedQuantity < 0))
            throw new ValidationException("Count lines and non-negative quantities are required.");
        if (input.Select(x => x.IngredientId).Distinct().Count() != input.Count)
            throw new ValidationException("Count lines cannot contain duplicate ingredients.");

        var expected = lines.Select(x => x.IngredientId).ToHashSet();
        if (input.Count != expected.Count || input.Any(x => !expected.Contains(x.IngredientId)))
            throw new ValidationException("All and only the count ingredients are required.");
    }

    private Guid UserId() => user.UserId ?? throw new UnauthorizedException("User is required.");
}
