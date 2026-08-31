using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Orders;

public class OrderCancellationService(IAppDbContext db, ICurrentUserService currentUser)
{
    public Task<List<OrderCancellationDto>> GetAsync(Guid branchId, DateTime? from, DateTime? to, Guid? cashierUserId, CancellationToken ct=default) =>
        db.OrderCancellations.Where(x=>x.Order.BranchId==branchId && (from==null||x.CreatedAt>=from) && (to==null||x.CreatedAt<to) && (cashierUserId==null||x.CancelledByUserId==cashierUserId))
            .OrderByDescending(x=>x.CreatedAt).Select(x=>new OrderCancellationDto(x.Id,x.OrderId,x.Order.OrderNumber,x.OrderItemId,x.OrderItem==null?null:x.OrderItem.MenuItemNameSnapshot,x.Reason,x.CancelledByUserId,x.CreatedAt)).ToListAsync(ct);

    public async Task CancelItemAsync(Guid orderId, Guid itemId, string reason, CancellationToken ct=default)
    {
        ValidateReason(reason); var userId=RequireUser();
        var order=await db.RestaurantOrders.Include(x=>x.Items).FirstOrDefaultAsync(x=>x.Id==orderId,ct)??throw new NotFoundException("Order not found.");
        EnsureOpen(order); var item=order.Items.SingleOrDefault(x=>x.Id==itemId)??throw new NotFoundException("Order item not found.");
        if(item.IsCancelled)throw new ValidationException("Order item is already cancelled.");
        item.IsCancelled=true; db.OrderCancellations.Add(new(){Id=Guid.NewGuid(),OrderId=order.Id,OrderItemId=item.Id,Reason=reason.Trim(),CancelledByUserId=userId,CreatedAt=DateTime.UtcNow});
        Recalculate(order); await db.SaveChangesAsync(ct);
    }

    public async Task CancelOrderAsync(Guid orderId, string reason, CancellationToken ct=default)
    {
        ValidateReason(reason); var userId=RequireUser();
        var order=await db.RestaurantOrders.Include(x=>x.Items).FirstOrDefaultAsync(x=>x.Id==orderId,ct)??throw new NotFoundException("Order not found."); EnsureOpen(order);
        foreach(var item in order.Items)item.IsCancelled=true; order.Status=RestaurantOrderStatuses.Cancelled; order.Subtotal=0; order.DiscountAmount=0; order.GrandTotal=0;
        db.OrderCancellations.Add(new(){Id=Guid.NewGuid(),OrderId=order.Id,Reason=reason.Trim(),CancelledByUserId=userId,CreatedAt=DateTime.UtcNow}); await db.SaveChangesAsync(ct);
    }

    private Guid RequireUser()=>currentUser.UserId??throw new ValidationException("Authenticated user is required.");
    private static void ValidateReason(string reason){if(string.IsNullOrWhiteSpace(reason)||reason.Trim().Length<3)throw new ValidationException("Cancellation reason must contain at least 3 characters.");}
    private static void EnsureOpen(RestaurantOrder order){if(order.Status is RestaurantOrderStatuses.Paid or RestaurantOrderStatuses.Closed or RestaurantOrderStatuses.Cancelled)throw new ValidationException("Paid, closed, or cancelled orders cannot be cancelled here.");}
    private static void Recalculate(RestaurantOrder order){order.Subtotal=order.Items.Where(x=>!x.IsCancelled).Sum(x=>x.LineTotal);order.DiscountAmount=Math.Min(order.DiscountAmount,order.Subtotal);order.GrandTotal=order.Subtotal-order.DiscountAmount;if(order.Items.All(x=>x.IsCancelled))order.Status=RestaurantOrderStatuses.Cancelled;}
}
