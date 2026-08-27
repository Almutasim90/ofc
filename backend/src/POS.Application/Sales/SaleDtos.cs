namespace POS.Application.Sales;

public record SaleLineRequest(Guid ProductId, decimal Quantity, string? DiscountType = null, decimal DiscountValue = 0);

public record CreateSaleRequest(
    Guid BranchId, string PaymentMethod, List<SaleLineRequest> Lines,
    string? DiscountType = null, decimal DiscountValue = 0, Guid? ChannelId = null, decimal? CashAmount = null, decimal? CardAmount = null);

public record SaleItemDto(
    Guid ProductId, string ProductNameSnapshot, decimal UnitPriceSnapshot, decimal Quantity, decimal LineTotal,
    string DiscountType, decimal DiscountValue);

public record SaleDto(
    Guid Id,
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
    List<SaleItemDto> Items, decimal CashAmount, decimal CardAmount, int Revision, bool CanEdit);

public record UpdateSaleRequest(CreateSaleRequest Sale, string Reason, int Revision);
public record SaleEditDto(Guid Id, Guid EditedByUserId, string EditedByName, DateTime CreatedAt, string Reason, SaleDto Before, SaleDto After);
