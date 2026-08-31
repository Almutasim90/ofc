using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Orders;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;
public class OrderCancellationTests
{
    [Fact]
    public async Task Cancelling_item_records_audit_and_recalculates_totals()
    {
        var user=new TestUser();await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,user);var ct=TestContext.Current.CancellationToken;
        var order=new RestaurantOrder{Id=Guid.NewGuid(),BranchId=user.BranchId!.Value,CashierUserId=user.UserId!.Value,Subtotal=5,GrandTotal=5,Status=RestaurantOrderStatuses.Open,Items=[new(){Id=Guid.NewGuid(),LineTotal=2,Quantity=1,MenuItemNameSnapshot="Burger"},new(){Id=Guid.NewGuid(),LineTotal=3,Quantity=1,MenuItemNameSnapshot="Meal"}]};db.RestaurantOrders.Add(order);await db.SaveChangesAsync(ct);
        var service=new OrderCancellationService(db,user);await service.CancelItemAsync(order.Id,order.Items.First().Id,"Customer changed mind",ct);
        Assert.True(order.Items.First().IsCancelled);Assert.Equal(3,order.GrandTotal);var audit=Assert.Single(db.OrderCancellations);Assert.Equal(user.UserId,audit.CancelledByUserId);
    }
    [Fact]
    public async Task Paid_order_cannot_be_cancelled()
    {
        var user=new TestUser();await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,user);var order=new RestaurantOrder{Id=Guid.NewGuid(),BranchId=user.BranchId!.Value,CashierUserId=user.UserId!.Value,Status=RestaurantOrderStatuses.Paid};db.RestaurantOrders.Add(order);await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ValidationException>(()=>new OrderCancellationService(db,user).CancelOrderAsync(order.Id,"Wrong order",TestContext.Current.CancellationToken));
    }
    private sealed class TestUser:ICurrentUserService{public Guid? UserId{get;}=Guid.NewGuid();public Guid? BranchId{get;}=Guid.NewGuid();public string? RoleName=>"GeneralManager";public IReadOnlyCollection<string> Permissions=>[];public bool BypassBranchFilter=>true;}
}
