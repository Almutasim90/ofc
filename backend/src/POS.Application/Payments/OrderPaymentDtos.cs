namespace POS.Application.Payments;
public record PaymentMethodDto(Guid Id,string Code,string NameAr,string NameEn,bool RequiresApproval,bool IsActive);
public record OrderPaymentDto(Guid Id,Guid OrderId,string MethodCode,decimal Amount,Guid? ApprovedByUserId,DateTime CreatedAt,string OrderStatus,decimal GrandTotal);
public record RecordOrderPaymentRequest(string PaymentMethodCode,decimal Amount);
public record EditClosedOrderRequest(string EditType,decimal AmountDelta,string? Notes);
public record OrderEditLogDto(Guid Id,Guid OrderId,Guid UserId,string EditType,string? Notes,decimal AmountDelta,DateTime CreatedAt,decimal OrderGrandTotal);
