using POS.Application.Orders;
namespace POS.Application.QrOrdering;

public record CarPickupBayDto(Guid Id, Guid BranchId, string BayLabel, bool IsActive);
public record SaveCarPickupBayRequest(Guid BranchId, string BayLabel, bool IsActive = true);
public record TransferOrderRequest(Guid NewOrderingPointId, string? Notes);
public record OrderingPointDto(Guid Id, Guid BranchId, string PointType, Guid? LinkedTableId, Guid? LinkedCarBayId, string QrToken, bool IsActive, string Label, Guid? ActiveSessionId);
public record SaveOrderingPointRequest(Guid BranchId, string PointType, Guid? LinkedTableId, Guid? LinkedCarBayId, bool IsActive = true);
public record QrSessionDto(Guid SessionId, Guid PointId, Guid BranchId, string PointType, string Label, DateTime OpenedAt, string AccessToken);
public record AddQrOrderRequest(string? AccessToken, List<CreateOrderLineRequest> Lines, string? QrCodeToken = null);
public record ConfirmQrOrderRequest(Guid SessionId, string? AccessToken, string? QrCodeToken = null);
public record QrMenuItemDto(Guid Id,Guid CategoryId,string NameAr,string NameEn,string Kind,decimal Price,string? ImageUrl);
public record QrMenuCategoryDto(Guid Id,string NameAr,string NameEn,List<QrMenuItemDto> Items);
