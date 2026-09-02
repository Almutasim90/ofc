using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.TableManagement;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class TableManagementTests
{
    [Fact]
    public async Task Floor_crud_is_branch_scoped_and_assigned_floor_cannot_be_deleted()
    {
        var branchId = Guid.NewGuid();
        await using var db = Db(new BranchUser(branchId));
        db.Branches.Add(new() { Id = branchId, Code = "B1", NameAr = "فرع", NameEn = "Branch" });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new TableManagementService(db, new BranchUser(branchId));

        var floor = await service.SaveFloorAsync(null, new(branchId, "Main", 0), TestContext.Current.CancellationToken);
        var updated = await service.SaveFloorAsync(floor.Id, new(branchId, "Dining room", 1), TestContext.Current.CancellationToken);
        await service.SaveTableAsync(null, new(branchId, "T1", 4, floor.Id, 20, 30, RestaurantTableShapes.Round), TestContext.Current.CancellationToken);

        Assert.Equal("Dining room", updated.Name);
        Assert.Single(await service.GetFloorsAsync(branchId, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ValidationException>(() => service.GetFloorsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ValidationException>(() => service.DeleteFloorAsync(floor.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Board_derives_occupancy_from_nonterminal_orders_and_open_qr_sessions()
    {
        var user = new GlobalUser();
        await using var db = Db(user);
        var branch = new Branch { Id = Guid.NewGuid(), Code = "B1", NameAr = "فرع", NameEn = "Branch" };
        var floor = new RestaurantFloor { Id = Guid.NewGuid(), BranchId = branch.Id, Branch = branch, Name = "Main" };
        var orderTable = Table(branch, floor, "T1", 10);
        var sessionTable = Table(branch, floor, "T2", 45);
        var freeTable = Table(branch, floor, "T3", 80);
        var point = new OrderingPoint { Id = Guid.NewGuid(), BranchId = branch.Id, Branch = branch, PointType = OrderingPointTypes.Table, LinkedTable = sessionTable, QrCodeToken = Guid.NewGuid().ToString("N") };
        db.AddRange(branch, floor, orderTable, sessionTable, freeTable, point);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var dineIn = await db.OrderTypes.SingleAsync(x => x.Code == "DINE_IN", TestContext.Current.CancellationToken);
        db.RestaurantOrders.Add(new() { Id = Guid.NewGuid(), BranchId = branch.Id, OrderNumber = 1, OrderTypeId = dineIn.Id, TableId = orderTable.Id, Status = RestaurantOrderStatuses.Sent, GrandTotal = 12 });
        db.RestaurantOrders.Add(new() { Id = Guid.NewGuid(), BranchId = branch.Id, OrderNumber = 2, OrderTypeId = dineIn.Id, TableId = freeTable.Id, Status = RestaurantOrderStatuses.Closed, GrandTotal = 8 });
        db.OrderingSessions.Add(new() { Id = Guid.NewGuid(), OrderingPointId = point.Id, Status = OrderingSessionStatuses.Open, AccessToken = Guid.NewGuid().ToString("N"), OpenedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var board = await new TableManagementService(db, user).GetBoardAsync(branch.Id, floor.Id, TestContext.Current.CancellationToken);

        Assert.True(board.Single(x => x.Label == "T1").IsOccupied);
        Assert.Single(board.Single(x => x.Label == "T1").Orders);
        Assert.True(board.Single(x => x.Label == "T2").IsOccupied);
        Assert.NotNull(board.Single(x => x.Label == "T2").OpenQrSessionId);
        Assert.False(board.Single(x => x.Label == "T3").IsOccupied);
    }

    private static RestaurantTable Table(Branch branch, RestaurantFloor floor, string label, int x) => new()
    {
        Id = Guid.NewGuid(), BranchId = branch.Id, Branch = branch, FloorId = floor.Id, Floor = floor,
        Label = label, Capacity = 4, PositionX = x, PositionY = 30, Shape = RestaurantTableShapes.Rectangle
    };

    private static AppDbContext Db(ICurrentUserService user)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, user);
        db.Database.EnsureCreated();
        return db;
    }

    private sealed class GlobalUser : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid(); public Guid? BranchId => null; public string? RoleName => null;
        public IReadOnlyCollection<string> Permissions => []; public bool BypassBranchFilter => true;
    }

    private sealed class BranchUser(Guid branchId) : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid(); public Guid? BranchId { get; } = branchId; public string? RoleName => null;
        public IReadOnlyCollection<string> Permissions => []; public bool BypassBranchFilter => false;
    }
}
