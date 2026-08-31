namespace POS.Application.Orders;
public record OrderTypeDto(Guid Id,string Code,string NameAr,string NameEn);
public record CreateOrderComboSelectionRequest(Guid ComboComponentId,Guid SelectedMenuItemId);
public record CreateOrderLineRequest(Guid MenuItemId,int Quantity,string? Notes,List<Guid> ModifierOptionIds,List<CreateOrderComboSelectionRequest> ComboSelections);
public record CreateRestaurantOrderRequest(Guid BranchId,Guid OrderTypeId,Guid? TableId,string? CarPlateNumber,decimal DiscountAmount,List<CreateOrderLineRequest> Lines);
public record OrderLineDto(Guid Id,Guid MenuItemId,string Name,decimal UnitPrice,int Quantity,decimal LineTotal,string? Notes,bool IsCancelled,List<Guid> ModifierOptionIds,List<Guid> ComboOptionIds);
public record RestaurantOrderDto(Guid Id,Guid BranchId,int OrderNumber,string OrderTypeCode,Guid? TableId,string? TableLabel,string Status,decimal Subtotal,decimal DiscountAmount,decimal GrandTotal,DateTime CreatedAt,List<OrderLineDto> Items);
public record CancelOrderRequest(string Reason);
public record OrderCancellationDto(Guid Id,Guid OrderId,int OrderNumber,Guid? OrderItemId,string? ItemName,string Reason,Guid CancelledByUserId,DateTime CreatedAt);
