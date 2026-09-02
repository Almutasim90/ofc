using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.TableManagement;

public class TableManagementService(IAppDbContext db, ICurrentUserService currentUser)
{
    public Task<List<RestaurantFloorDto>> GetFloorsAsync(Guid branchId, CancellationToken ct = default)
    {
        branchId = ScopedBranch(branchId);
        return db.RestaurantFloors.Where(x => x.BranchId == branchId).OrderBy(x => x.SortOrder).ThenBy(x => x.Name)
            .Select(x => new RestaurantFloorDto(x.Id, x.BranchId, x.Name, x.SortOrder, x.IsActive)).ToListAsync(ct);
    }

    public async Task<RestaurantFloorDto> SaveFloorAsync(Guid? id, SaveRestaurantFloorRequest request, CancellationToken ct = default)
    {
        var branchId = ScopedBranch(request.BranchId);
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ValidationException("Floor name is required.");
        if (request.SortOrder < 0) throw new ValidationException("Floor sort order cannot be negative.");
        if (!await db.Branches.AnyAsync(x => x.Id == branchId, ct)) throw new NotFoundException("Branch not found.");
        var name = request.Name.Trim();
        if (await db.RestaurantFloors.AnyAsync(x => x.BranchId == branchId && x.Name == name && x.Id != id, ct)) throw new ValidationException("Floor name already exists in this branch.");
        var floor = id is null
            ? new RestaurantFloor { Id = Guid.NewGuid(), BranchId = branchId }
            : await db.RestaurantFloors.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId, ct) ?? throw new NotFoundException("Floor not found.");
        if (id is null) db.RestaurantFloors.Add(floor);
        floor.Name = name; floor.SortOrder = request.SortOrder; floor.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new(floor.Id, floor.BranchId, floor.Name, floor.SortOrder, floor.IsActive);
    }

    public async Task DeleteFloorAsync(Guid id, CancellationToken ct = default)
    {
        var floor = await db.RestaurantFloors.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Floor not found.");
        ScopedBranch(floor.BranchId);
        if (await db.RestaurantTables.AnyAsync(x => x.FloorId == id, ct)) throw new ValidationException("Move or remove the floor's tables before deleting it.");
        db.RestaurantFloors.Remove(floor);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<TableLayoutDto>> GetTablesAsync(Guid branchId, Guid? floorId = null, CancellationToken ct = default)
    {
        branchId = ScopedBranch(branchId);
        return db.RestaurantTables.Where(x => x.BranchId == branchId && (floorId == null || x.FloorId == floorId))
            .OrderBy(x => x.Label).Select(x => new TableLayoutDto(x.Id, x.BranchId, x.Label, x.Capacity,
                x.IsActive, x.FloorId, x.Floor == null ? null : x.Floor.Name, x.PositionX, x.PositionY, x.Shape)).ToListAsync(ct);
    }

    public async Task<TableLayoutDto> SaveTableAsync(Guid? id, SaveTableLayoutRequest request, CancellationToken ct = default)
    {
        var branchId = ScopedBranch(request.BranchId);
        if (string.IsNullOrWhiteSpace(request.Label)) throw new ValidationException("Table label is required.");
        if (request.Capacity is <= 0) throw new ValidationException("Capacity must be greater than zero.");
        if (request.PositionX is < 0 or > 100 || request.PositionY is < 0 or > 100) throw new ValidationException("Table positions must be between 0 and 100.");
        if (!RestaurantTableShapes.All.Contains(request.Shape)) throw new ValidationException("Table shape is invalid.");
        if (!await db.Branches.AnyAsync(x => x.Id == branchId, ct)) throw new NotFoundException("Branch not found.");
        RestaurantFloor? floor = null;
        if (request.FloorId is not null) floor = await db.RestaurantFloors.FirstOrDefaultAsync(x => x.Id == request.FloorId && x.BranchId == branchId, ct) ?? throw new ValidationException("Floor is invalid for this branch.");
        var label = request.Label.Trim();
        if (await db.RestaurantTables.AnyAsync(x => x.BranchId == branchId && x.Label == label && x.Id != id, ct)) throw new ValidationException("Table label already exists in this branch.");
        var table = id is null
            ? new RestaurantTable { Id = Guid.NewGuid(), BranchId = branchId }
            : await db.RestaurantTables.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId, ct) ?? throw new NotFoundException("Table not found.");
        if (id is null) db.RestaurantTables.Add(table);
        table.Label = label; table.Capacity = request.Capacity; table.FloorId = request.FloorId;
        table.PositionX = request.PositionX; table.PositionY = request.PositionY; table.Shape = request.Shape; table.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new(table.Id, table.BranchId, table.Label, table.Capacity, table.IsActive, table.FloorId, floor?.Name, table.PositionX, table.PositionY, table.Shape);
    }

    public async Task<List<TableStatusDto>> GetBoardAsync(Guid branchId, Guid? floorId = null, CancellationToken ct = default)
    {
        branchId = ScopedBranch(branchId);
        var tables = await GetTablesAsync(branchId, floorId, ct);
        var tableIds = tables.Select(x => x.Id).ToList();
        var orders = await db.RestaurantOrders.Where(x => x.TableId != null && tableIds.Contains(x.TableId.Value)
                && x.Status != RestaurantOrderStatuses.Closed && x.Status != RestaurantOrderStatuses.Cancelled)
            .OrderBy(x => x.CreatedAt).Select(x => new { TableId = x.TableId!.Value, Order = new TableOrderStatusDto(
                x.Id, x.OrderNumber, x.Status, x.OrderType.Code, x.GrandTotal, x.CreatedAt) }).ToListAsync(ct);
        var sessions = await db.OrderingSessions.Where(x => x.Status == OrderingSessionStatuses.Open
                && x.OrderingPoint.LinkedTableId != null && tableIds.Contains(x.OrderingPoint.LinkedTableId.Value))
            .Select(x => new { TableId = x.OrderingPoint.LinkedTableId!.Value, x.Id, x.OpenedAt }).ToListAsync(ct);

        return tables.Select(table =>
        {
            var tableOrders = orders.Where(x => x.TableId == table.Id).Select(x => x.Order).ToList();
            var session = sessions.FirstOrDefault(x => x.TableId == table.Id);
            return new TableStatusDto(table.Id, table.BranchId, table.Label, table.Capacity, table.IsActive,
                table.FloorId, table.FloorName, table.PositionX, table.PositionY, table.Shape,
                tableOrders.Count > 0 || session is not null, session?.Id, session?.OpenedAt, tableOrders);
        }).ToList();
    }

    private Guid ScopedBranch(Guid requested)
    {
        if (!currentUser.BypassBranchFilter && currentUser.BranchId != requested) throw new ValidationException("You do not have access to this branch.");
        return currentUser.BypassBranchFilter ? requested : currentUser.BranchId ?? throw new ValidationException("A branch assignment is required.");
    }
}
