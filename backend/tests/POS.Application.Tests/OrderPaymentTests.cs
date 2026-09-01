using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Payments;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class OrderPaymentTests
{
    [Fact]
    public async Task Split_cash_and_card_marks_order_paid()
    {
        await using var db = Db();
        var order = AddOrder(db, 10);
        var service = new OrderPaymentService(db, new User([]));

        var first = await service.RecordAsync(order.Id, new("CASH", 4), TestContext.Current.CancellationToken);
        Assert.Equal(RestaurantOrderStatuses.Open, first.OrderStatus);
        var second = await service.RecordAsync(order.Id, new("CARD", 6), TestContext.Current.CancellationToken);

        Assert.Equal(RestaurantOrderStatuses.Paid, second.OrderStatus);
        Assert.Equal(10, db.OrderPayments.Sum(x => x.Amount));
        Assert.Equal(2, order.PaymentRevision);
    }

    [Fact]
    public async Task Debt_without_permission_is_rejected()
    {
        await using var db = Db();
        var order = AddOrder(db, 10);
        var service = new OrderPaymentService(db, new User([]));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.RecordAsync(order.Id, new("DEBT", 10), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Debt_approval_records_the_approving_user()
    {
        await using var db = Db();
        var order = AddOrder(db, 10);
        var user = new User([PermissionKeys.DebtPaymentsApprove]);

        var payment = await new OrderPaymentService(db, user)
            .RecordAsync(order.Id, new("DEBT", 10), TestContext.Current.CancellationToken);

        Assert.Equal(user.UserId, payment.ApprovedByUserId);
    }

    [Fact]
    public async Task Payment_cannot_exceed_remaining_balance()
    {
        await using var db = Db();
        var order = AddOrder(db, 10);

        await Assert.ThrowsAsync<ValidationException>(() => new OrderPaymentService(db, new User([]))
            .RecordAsync(order.Id, new("CASH", 10.001m), TestContext.Current.CancellationToken));

        Assert.Empty(db.OrderPayments);
        Assert.Equal(0, order.PaymentRevision);
    }

    [Fact]
    public async Task Closed_order_price_override_changes_total_and_records_audit()
    {
        await using var db = Db();
        var order = AddOrder(db, 10, RestaurantOrderStatuses.Closed);
        var user = new User([PermissionKeys.ClosedOrdersEdit]);

        var log = await new OrderPaymentService(db, user)
            .EditAsync(order.Id, new("PriceOverride", 2, "manager correction"), TestContext.Current.CancellationToken);

        Assert.Equal(12, order.GrandTotal);
        Assert.Equal(12, log.OrderGrandTotal);
        Assert.Equal(user.UserId, log.UserId);
        Assert.Equal(1, order.PaymentRevision);
        Assert.Single(db.OrderEditLogs);
    }

    [Fact]
    public async Task Unsupported_invoice_edit_does_not_create_fictitious_audit()
    {
        await using var db = Db();
        var order = AddOrder(db, 10, RestaurantOrderStatuses.Closed);
        var service = new OrderPaymentService(db, new User([PermissionKeys.ClosedOrdersEdit]));

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.EditAsync(order.Id, new("ItemRemoved", -2, null), TestContext.Current.CancellationToken));

        Assert.Equal(10, order.GrandTotal);
        Assert.Empty(db.OrderEditLogs);
    }

    private static AppDbContext Db()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static RestaurantOrder AddOrder(AppDbContext db, decimal total, string status = RestaurantOrderStatuses.Open)
    {
        var branchId = Guid.NewGuid();
        var order = new RestaurantOrder { Id = Guid.NewGuid(), BranchId = branchId, GrandTotal = total, Subtotal = total, Status = status };
        db.RestaurantOrders.Add(order);
        db.CashShifts.Add(new CashShift { Id = Guid.NewGuid(), BranchId = branchId, OpenedByUserId = Guid.NewGuid(), OpenedAt = DateTime.UtcNow });
        db.SaveChanges();
        return order;
    }

    private sealed class User(IReadOnlyCollection<string> permissions) : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? BranchId => null;
        public string? RoleName => "GeneralManager";
        public IReadOnlyCollection<string> Permissions => permissions;
        public bool BypassBranchFilter => true;
    }
}
