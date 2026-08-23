namespace POS.Application.Sales;

public record SaleLineRequest(Guid ProductId, decimal Quantity);

public record CreateSaleRequest(Guid BranchId, string PaymentMethod, List<SaleLineRequest> Lines);

public record SaleItemDto(
    Guid ProductId, string ProductNameSnapshot, decimal UnitPriceSnapshot, decimal Quantity, decimal LineTotal);

public record SaleDto(
    Guid Id,
    Guid BranchId,
    Guid CashierUserId,
    DateOnly BusinessDate,
    DateTime CreatedAt,
    decimal TotalAmount,
    string PaymentMethod,
    string Status,
    List<SaleItemDto> Items);
