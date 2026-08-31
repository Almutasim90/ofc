using Microsoft.EntityFrameworkCore;using POS.Application.Abstractions;using POS.Application.Common;using POS.Application.Payments;using POS.Domain.Constants;using POS.Domain.Entities;using POS.Infrastructure.Persistence;using Xunit;
namespace POS.Application.Tests;
public class OrderPaymentTests
{
 [Fact]public async Task Split_cash_and_card_marks_order_paid(){await using var db=Db();var order=AddOrder(db,10);var user=new User([]);var service=new OrderPaymentService(db,user);await service.RecordAsync(order.Id,new("CASH",4));Assert.Equal(RestaurantOrderStatuses.Open,order.Status);await service.RecordAsync(order.Id,new("CARD",6));Assert.Equal(RestaurantOrderStatuses.Paid,order.Status);Assert.Equal(10,db.OrderPayments.Sum(x=>x.Amount));}
 [Fact]public async Task Debt_without_permission_is_rejected(){await using var db=Db();var order=AddOrder(db,10);var service=new OrderPaymentService(db,new User([]));await Assert.ThrowsAsync<ForbiddenException>(()=>service.RecordAsync(order.Id,new("DEBT",10)));}
 [Fact]public async Task Closed_order_edit_changes_total_and_records_audit(){await using var db=Db();var order=AddOrder(db,10,RestaurantOrderStatuses.Closed);var user=new User([PermissionKeys.ClosedOrdersEdit]);var log=await new OrderPaymentService(db,user).EditAsync(order.Id,new("PriceOverride",2,"manager correction"));Assert.Equal(12,order.GrandTotal);Assert.Equal(user.UserId,log.UserId);Assert.Single(db.OrderEditLogs);}
 private static AppDbContext Db(){var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);db.Database.EnsureCreated();return db;}
 private static RestaurantOrder AddOrder(AppDbContext db,decimal total,string status=RestaurantOrderStatuses.Open){var x=new RestaurantOrder{Id=Guid.NewGuid(),GrandTotal=total,Subtotal=total,Status=status};db.RestaurantOrders.Add(x);db.SaveChanges();return x;}
 private sealed class User(IReadOnlyCollection<string> permissions):ICurrentUserService{public Guid? UserId{get;}=Guid.NewGuid();public Guid? BranchId=>null;public string? RoleName=>"GeneralManager";public IReadOnlyCollection<string> Permissions=>permissions;public bool BypassBranchFilter=>true;}
}
