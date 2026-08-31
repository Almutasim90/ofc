using POS.Application.Orders;
namespace POS.Application.QrOrdering;
public record CarPickupBayDto(Guid Id,Guid BranchId,string BayLabel,bool IsActive);
public record SaveCarPickupBayRequest(Guid BranchId,string BayLabel,bool IsActive=true);
public record TransferOrderRequest(Guid NewOrderingPointId, string? Notes);
public record OrderingPointDto(Guid Id,Guid BranchId,string PointType,Guid? LinkedTableId,Guid? LinkedCarBayId,string QrCodeToken,bool IsActive,string Label,Guid? ActiveSessionId);
public record SaveOrderingPointRequest(Guid BranchId,string PointType,Guid? LinkedTableId,Guid? LinkedCarBayId,bool IsActive=true);
public record QrSessionDto(Guid SessionId,Guid PointId,Guid BranchId,string PointType,string Label,DateTime OpenedAt);
public record AddQrOrderRequest(string SalesChannelCode,List<CreateOrderLineRequest> Lines);
