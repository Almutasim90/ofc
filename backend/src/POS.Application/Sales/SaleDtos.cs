namespace POS.Application.Sales;

public record SaleLineRequest(Guid ProductId, decimal Quantity, string? DiscountType = null, decimal DiscountValue = 0);

public record CreateSaleRequest(
    Guid BranchId, string PaymentMethod, List<SaleLineRequest> Lines,
    string? DiscountType = null, decimal DiscountValue = 0, Guid? ChannelId = null);

public record SaleItemDto(
    Guid ProductId, string ProductNameSnapshot, decimal UnitPriceSnapshot, decimal Quantity, decimal LineTotal,
    string DiscountType, decimal DiscountValue);

public record SaleDto(
    Guid Id,
    int SaleNumber,
    Guid BranchId,
    Guid ChannelId,
    Guid ShiftId,
    Guid CashierUserId,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    decimal TotalAmount,
    string DiscountType,
    decimal DiscountValue,
    decimal DiscountAmount,
    string PaymentMethod,
    string Status,
    List<SaleItemDto> Items);
