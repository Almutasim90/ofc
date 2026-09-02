using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Application.Invoices;

namespace POS.Application.Payments;

public class OrderPaymentService(IAppDbContext db, ICurrentUserService currentUser)
{
    private const string SupportedEditType = "PriceOverride";
    public Task<List<PaymentMethodDto>> MethodsAsync(CancellationToken ct=default)=>db.PaymentMethods.Where(x=>x.IsActive).OrderBy(x=>x.Code).Select(x=>new PaymentMethodDto(x.Id,x.Code,x.NameAr,x.NameEn,x.RequiresApproval,x.IsActive)).ToListAsync(ct);
    public Task<List<OrderPaymentDto>> PaymentsAsync(Guid orderId,CancellationToken ct=default)=>db.OrderPayments.Where(x=>x.OrderId==orderId).OrderBy(x=>x.CreatedAt).Select(x=>new OrderPaymentDto(x.Id,x.OrderId,x.PaymentMethod.Code,x.Amount,x.ApprovedByUserId,x.CreatedAt,x.Order.Status,x.Order.GrandTotal,x.BillSplitId)).ToListAsync(ct);
    public Task<List<OrderEditLogDto>> EditsAsync(Guid orderId,CancellationToken ct=default)=>db.OrderEditLogs.Where(x=>x.OrderId==orderId).OrderByDescending(x=>x.CreatedAt).Select(x=>new OrderEditLogDto(x.Id,x.OrderId,x.UserId,x.EditType,x.Notes,x.AmountDelta,x.CreatedAt,x.Order.GrandTotal)).ToListAsync(ct);
    public async Task<OrderPaymentDto> RecordAsync(Guid orderId,RecordOrderPaymentRequest request,CancellationToken ct=default)
    {
        if(request.Amount<=0)throw new ValidationException("Payment amount must be positive."); var userId=currentUser.UserId??throw new UnauthorizedException("Authenticated user is required.");
        var order=await db.RestaurantOrders.Include(x=>x.Payments).Include(x=>x.BillSplits).FirstOrDefaultAsync(x=>x.Id==orderId,ct)??throw new NotFoundException("Order not found.");
        if(order.Status is RestaurantOrderStatuses.Cancelled or RestaurantOrderStatuses.Closed)throw new ValidationException("This order cannot accept payments.");
        if(order.BillSplits.Count>0&&!request.BillSplitId.HasValue)throw new ValidationException("Select a bill split for this payment.");
        BillSplit? split=null;if(request.BillSplitId.HasValue){split=await db.BillSplits.Include(x=>x.Payments).FirstOrDefaultAsync(x=>x.Id==request.BillSplitId&&x.OrderId==order.Id,ct)??throw new NotFoundException("Bill split not found.");var splitRemaining=split.Amount-split.Payments.Sum(x=>x.Amount);if(request.Amount>splitRemaining)throw new ValidationException("Payment exceeds the remaining split balance.");}
        var code=request.PaymentMethodCode.Trim().ToUpperInvariant();var method=await db.PaymentMethods.FirstOrDefaultAsync(x=>x.Code==code&&x.IsActive,ct)??throw new NotFoundException("Payment method not found.");
        if(method.RequiresApproval&&!currentUser.Permissions.Contains(PermissionKeys.DebtPaymentsApprove))throw new ForbiddenException("Debt payment approval permission is required.");
        var remaining=order.GrandTotal-order.Payments.Sum(x=>x.Amount);if(request.Amount>remaining)throw new ValidationException("Payment exceeds the remaining balance.");
        var cashShiftId=code=="CASH"?await db.CashShifts.Where(x=>x.BranchId==order.BranchId&&x.Status==CashShiftStatuses.Open).Select(x=>(Guid?)x.Id).SingleOrDefaultAsync(ct):null;if(code=="CASH"&&cashShiftId is null)throw new ValidationException("An open cash shift is required for cash payments.");var row=new OrderPayment{Id=Guid.NewGuid(),OrderId=order.Id,BillSplitId=split?.Id,PaymentMethodId=method.Id,CashShiftId=cashShiftId,Amount=request.Amount,ApprovedByUserId=method.RequiresApproval?userId:null,CreatedAt=DateTime.UtcNow};db.OrderPayments.Add(row);
        if(request.Amount==remaining&&(order.OrderingSessionId is null||order.Status==RestaurantOrderStatuses.Sent)){order.Status=RestaurantOrderStatuses.Paid;InvoiceService.CaptureCompletedSnapshot(order);}order.PaymentRevision++;await db.SaveChangesAsync(ct);return new(row.Id,row.OrderId,method.Code,row.Amount,row.ApprovedByUserId,row.CreatedAt,order.Status,order.GrandTotal,row.BillSplitId);
    }
    public async Task<OrderEditLogDto> EditAsync(Guid orderId,EditClosedOrderRequest request,CancellationToken ct=default)
    {
        if(!currentUser.Permissions.Contains(PermissionKeys.ClosedOrdersEdit))throw new ForbiddenException("Closed order edit permission is required."); if(request.EditType!=SupportedEditType)throw new ValidationException("Only price overrides are currently supported.");
        var userId=currentUser.UserId??throw new UnauthorizedException("Authenticated user is required.");var order=await db.RestaurantOrders.Include(x=>x.Payments).Include(x=>x.BillSplits).FirstOrDefaultAsync(x=>x.Id==orderId,ct)??throw new NotFoundException("Order not found.");
        if(order.Status is not (RestaurantOrderStatuses.Paid or RestaurantOrderStatuses.Closed))throw new ValidationException("Only paid or closed orders can be edited.");var updated=order.GrandTotal+request.AmountDelta;if(updated<0||updated<order.Payments.Sum(x=>x.Amount)||updated<order.BillSplits.Sum(x=>x.Amount))throw new ValidationException("Edited total cannot be negative or less than payments or split allocations already recorded.");
        if(request.AmountDelta==0)throw new ValidationException("Price override amount cannot be zero.");order.GrandTotal=updated;order.Subtotal+=request.AmountDelta;order.PaymentRevision++;var log=new OrderEditLog{Id=Guid.NewGuid(),OrderId=order.Id,UserId=userId,EditType=request.EditType,Notes=string.IsNullOrWhiteSpace(request.Notes)?null:request.Notes.Trim(),AmountDelta=request.AmountDelta,CreatedAt=DateTime.UtcNow};db.OrderEditLogs.Add(log);await db.SaveChangesAsync(ct);return new(log.Id,log.OrderId,log.UserId,log.EditType,log.Notes,log.AmountDelta,log.CreatedAt,order.GrandTotal);
    }
}
