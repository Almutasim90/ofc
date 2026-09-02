using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Payments;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class BillSplitTests
{
    [Fact]
    public async Task Equal_splits_allocate_the_exact_order_total()
    {
        await using var db = Db();
        var order = AddOrder(db, 10.001m);

        var splits = await new BillSplitService(db).CreateEqualAsync(order.Id, new(3), TestContext.Current.CancellationToken);

        Assert.Equal([3.333m, 3.333m, 3.335m], splits.Select(x => x.Amount));
        Assert.Equal(order.GrandTotal, splits.Sum(x => x.Amount));
        Assert.Single(db.RestaurantOrders);
    }

    [Fact]
    public async Task Item_split_allocates_quantities_and_respects_order_discount()
    {
        await using var db = Db();
        var order = AddOrder(db, 20);
        var item = AddItem(db, order, "Dinner", 10, 2);
        AddItem(db, order, "Drink", 5, 1);
        db.SaveChanges();

        var split = await new BillSplitService(db).CreateItemAsync(order.Id,
            new("Guest 1", [new(item.Id, 1)]), TestContext.Current.CancellationToken);

        Assert.Equal(8, split.Amount);
        Assert.Equal(1, Assert.Single(split.Lines).Quantity);
        Assert.Equal(item.Id, Assert.Single(db.BillSplitLines).OrderItemId);
        Assert.Single(db.RestaurantOrders);
    }

    [Fact]
    public async Task Item_quantity_cannot_be_allocated_more_than_once()
    {
        await using var db = Db();
        var order = AddOrder(db, 30);
        var item = AddItem(db, order, "Dinner", 10, 2);
        AddItem(db, order, "Drink", 10, 1);
        db.SaveChanges();
        var service = new BillSplitService(db);
        await service.CreateItemAsync(order.Id, new("First", [new(item.Id, 1)]), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateItemAsync(order.Id,
            new("Second", [new(item.Id, 2)]), TestContext.Current.CancellationToken));

        Assert.Single(db.BillSplits);
        Assert.Equal(1, db.BillSplitLines.Sum(x => x.Quantity));
    }

    [Fact]
    public async Task Paying_each_split_settles_the_split_then_the_order()
    {
        await using var db = Db();
        var order = AddOrder(db, 10);
        var splits = await new BillSplitService(db).CreateEqualAsync(order.Id, new(2), TestContext.Current.CancellationToken);
        var payments = new OrderPaymentService(db, new User());

        var first = await payments.RecordAsync(order.Id, new("CARD", 5, splits[0].Id), TestContext.Current.CancellationToken);
        Assert.Equal(RestaurantOrderStatuses.Open, first.OrderStatus);
        Assert.Equal(splits[0].Id, first.BillSplitId);
        Assert.Equal(0, (await new BillSplitService(db).ListAsync(order.Id, TestContext.Current.CancellationToken))[0].RemainingAmount);

        var second = await payments.RecordAsync(order.Id, new("CARD", 5, splits[1].Id), TestContext.Current.CancellationToken);
        Assert.Equal(RestaurantOrderStatuses.Paid, second.OrderStatus);
        Assert.All(await new BillSplitService(db).ListAsync(order.Id, TestContext.Current.CancellationToken), x => Assert.Equal(0, x.RemainingAmount));
    }

    [Fact]
    public async Task Split_orders_require_payments_to_target_a_split()
    {
        await using var db = Db();
        var order = AddOrder(db, 10);
        await new BillSplitService(db).CreateEqualAsync(order.Id, new(2), TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ValidationException>(() => new OrderPaymentService(db, new User()).RecordAsync(
            order.Id, new("CARD", 5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Final_payment_marks_an_approved_qr_order_paid()
    {
        await using var db = Db();
        var order = AddOrder(db, 10);
        order.OrderingSessionId = Guid.NewGuid();
        order.Status = RestaurantOrderStatuses.Sent;
        db.SaveChanges();

        var payment = await new OrderPaymentService(db, new User()).RecordAsync(
            order.Id, new("CARD", 10), TestContext.Current.CancellationToken);

        Assert.Equal(RestaurantOrderStatuses.Paid, payment.OrderStatus);
        Assert.NotNull(order.InvoiceSnapshotCapturedAt);
    }

    private static AppDbContext Db()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static RestaurantOrder AddOrder(AppDbContext db, decimal total)
    {
        var order = new RestaurantOrder { Id = Guid.NewGuid(), BranchId = Guid.NewGuid(), GrandTotal = total, Subtotal = total, Status = RestaurantOrderStatuses.Open };
        db.RestaurantOrders.Add(order);
        db.SaveChanges();
        return order;
    }

    private static RestaurantOrderItem AddItem(AppDbContext db, RestaurantOrder order, string name, decimal unitPrice, int quantity)
    {
        var item = new RestaurantOrderItem { Id = Guid.NewGuid(), OrderId = order.Id, MenuItemNameSnapshot = name, UnitPriceSnapshot = unitPrice, Quantity = quantity, LineTotal = unitPrice * quantity };
        order.Items.Add(item);
        db.RestaurantOrderItems.Add(item);
        return item;
    }

    private sealed class User : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? BranchId => null;
        public string? RoleName => "GeneralManager";
        public IReadOnlyCollection<string> Permissions => [];
        public bool BypassBranchFilter => true;
    }
}
