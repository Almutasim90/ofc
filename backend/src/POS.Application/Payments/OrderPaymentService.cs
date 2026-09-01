using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Application.Payments;

public class OrderPaymentService(IAppDbContext db, ICurrentUserService currentUser)
{
    private const string SupportedEditType = "PriceOverride";
    public Task<List<PaymentMethodDto>> MethodsAsync(CancellationToken ct=default)=>db.PaymentMethods.Where(x=>x.IsActive).OrderBy(x=>x.Code).Select(x=>new PaymentMethodDto(x.Id,x.Code,x.NameAr,x.NameEn,x.RequiresApproval,x.IsActive)).ToListAsync(ct);
    public Task<List<OrderPaymentDto>> PaymentsAsync(Guid orderId,CancellationToken ct=default)=>db.OrderPayments.Where(x=>x.OrderId==orderId).OrderBy(x=>x.CreatedAt).Select(x=>new OrderPaymentDto(x.Id,x.OrderId,x.PaymentMethod.Code,x.Amount,x.ApprovedByUserId,x.CreatedAt,x.Order.Status,x.Order.GrandTotal)).ToListAsync(ct);
    public Task<List<OrderEditLogDto>> EditsAsync(Guid orderId,CancellationToken ct=default)=>db.OrderEditLogs.Where(x=>x.OrderId==orderId).OrderByDescending(x=>x.CreatedAt).Select(x=>new OrderEditLogDto(x.Id,x.OrderId,x.UserId,x.EditType,x.Notes,x.AmountDelta,x.CreatedAt,x.Order.GrandTotal)).ToListAsync(ct);
    public async Task<OrderPaymentDto> RecordAsync(Guid orderId,RecordOrderPaymentRequest request,CancellationToken ct=default)
    {
        if(request.Amount<=0)throw new ValidationException("Payment amount must be positive."); var userId=currentUser.UserId??throw new UnauthorizedException("Authenticated user is required.");
        var order=await db.RestaurantOrders.Include(x=>x.Payments).FirstOrDefaultAsync(x=>x.Id==orderId,ct)??throw new NotFoundException("Order not found.");
        if(order.Status is RestaurantOrderStatuses.Cancelled or RestaurantOrderStatuses.Closed)throw new ValidationException("This order cannot accept payments.");
        var code=request.PaymentMethodCode.Trim().ToUpperInvariant();var method=await db.PaymentMethods.FirstOrDefaultAsync(x=>x.Code==code&&x.IsActive,ct)??throw new NotFoundException("Payment method not found.");
        if(method.RequiresApproval&&!currentUser.Permissions.Contains(PermissionKeys.DebtPaymentsApprove))throw new ForbiddenException("Debt payment approval permission is required.");
        var remaining=order.GrandTotal-order.Payments.Sum(x=>x.Amount);if(request.Amount>remaining)throw new ValidationException("Payment exceeds the remaining balance.");
        var row=new OrderPayment{Id=Guid.NewGuid(),OrderId=order.Id,PaymentMethodId=method.Id,Amount=request.Amount,ApprovedByUserId=method.RequiresApproval?userId:null,CreatedAt=DateTime.UtcNow};db.OrderPayments.Add(row);
        if(request.Amount==remaining)order.Status=RestaurantOrderStatuses.Paid;order.PaymentRevision++;await db.SaveChangesAsync(ct);return new(row.Id,row.OrderId,method.Code,row.Amount,row.ApprovedByUserId,row.CreatedAt,order.Status,order.GrandTotal);
    }
    public async Task<OrderEditLogDto> EditAsync(Guid orderId,EditClosedOrderRequest request,CancellationToken ct=default)
    {
        if(!currentUser.Permissions.Contains(PermissionKeys.ClosedOrdersEdit))throw new ForbiddenException("Closed order edit permission is required."); if(request.EditType!=SupportedEditType)throw new ValidationException("Only price overrides are currently supported.");
        var userId=currentUser.UserId??throw new UnauthorizedException("Authenticated user is required.");var order=await db.RestaurantOrders.Include(x=>x.Payments).FirstOrDefaultAsync(x=>x.Id==orderId,ct)??throw new NotFoundException("Order not found.");
        if(order.Status is not (RestaurantOrderStatuses.Paid or RestaurantOrderStatuses.Closed))throw new ValidationException("Only paid or closed orders can be edited.");var updated=order.GrandTotal+request.AmountDelta;if(updated<0||updated<order.Payments.Sum(x=>x.Amount))throw new ValidationException("Edited total cannot be negative or less than payments already recorded.");
        if(request.AmountDelta==0)throw new ValidationException("Price override amount cannot be zero.");order.GrandTotal=updated;order.Subtotal+=request.AmountDelta;order.PaymentRevision++;var log=new OrderEditLog{Id=Guid.NewGuid(),OrderId=order.Id,UserId=userId,EditType=request.EditType,Notes=string.IsNullOrWhiteSpace(request.Notes)?null:request.Notes.Trim(),AmountDelta=request.AmountDelta,CreatedAt=DateTime.UtcNow};db.OrderEditLogs.Add(log);await db.SaveChangesAsync(ct);return new(log.Id,log.OrderId,log.UserId,log.EditType,log.Notes,log.AmountDelta,log.CreatedAt,order.GrandTotal);
    }
}
