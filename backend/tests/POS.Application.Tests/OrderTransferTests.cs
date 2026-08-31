using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Printing;
using POS.Application.QrOrdering;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class OrderTransferTests
{
    [Fact]
    public async Task Transfer_creates_target_session_and_audit_log()
    {
        await using var db = Db();
        var (order, point) = Seed(db, RestaurantOrderStatuses.Open);
        var userId = Guid.NewGuid();

        await Service(db).TransferOrderAsync(order.Id, point.Id, userId, "Guest requested another table", TestContext.Current.CancellationToken);

        var session = Assert.Single(db.OrderingSessions);
        Assert.Equal(session.Id, order.OrderingSessionId);
        var log = Assert.Single(db.OrderEditLogs);
        Assert.Equal("Transferred", log.EditType);
        Assert.Equal(userId, log.UserId);
        Assert.Equal(0, log.AmountDelta);
        Assert.Contains(point.Id.ToString(), log.Notes);
    }

    [Theory]
    [InlineData(RestaurantOrderStatuses.Paid)]
    [InlineData(RestaurantOrderStatuses.Closed)]
    public async Task Transfer_rejects_paid_or_closed_orders(string status)
    {
        await using var db = Db();
        var (order, point) = Seed(db, status);

        await Assert.ThrowsAsync<ValidationException>(() => Service(db).TransferOrderAsync(
            order.Id, point.Id, Guid.NewGuid(), null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Transfer_rejects_target_with_another_open_invoice()
    {
        await using var db = Db();
        var (order, point) = Seed(db, RestaurantOrderStatuses.Open);
        var session = new OrderingSession { Id = Guid.NewGuid(), OrderingPointId = point.Id, Status = OrderingSessionStatuses.Open, OpenedAt = DateTime.UtcNow };
        db.OrderingSessions.Add(session);
        db.RestaurantOrders.Add(new RestaurantOrder { Id = Guid.NewGuid(), BranchId = order.BranchId, OrderingSessionId = session.Id, Status = RestaurantOrderStatuses.Open });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ValidationException>(() => Service(db).TransferOrderAsync(
            order.Id, point.Id, Guid.NewGuid(), null, TestContext.Current.CancellationToken));
    }

    private static (RestaurantOrder Order, OrderingPoint Point) Seed(AppDbContext db, string status)
    {
        var branchId = Guid.NewGuid();
        var table = new RestaurantTable { Id = Guid.NewGuid(), BranchId = branchId, Label = "T2" };
        var point = new OrderingPoint { Id = Guid.NewGuid(), BranchId = branchId, PointType = OrderingPointTypes.Table, LinkedTable = table, QrCodeToken = Guid.NewGuid().ToString("N"), IsActive = true };
        var order = new RestaurantOrder { Id = Guid.NewGuid(), BranchId = branchId, OrderNumber = 1, Status = status };
        db.AddRange(table, point, order);
        db.SaveChanges();
        return (order, point);
    }

    private static AppDbContext Db()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static QrOrderingService Service(AppDbContext db) => new(db, new OrderPrintingService(db, new Printer()));

    private sealed class Printer : IRawPrinterClient
    {
        public Task SendAsync(string ipAddress, int port, byte[] payload, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
