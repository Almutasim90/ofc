namespace POS.Application.Shifts;

public record OpenShiftRequest(Guid BranchId, decimal? OpeningCash = null);
public record CashCountLineRequest(decimal Denomination, int Quantity);
public record CloseShiftRequest(IReadOnlyList<CashCountLineRequest> Counts);
public record ShiftCashCountDto(decimal Denomination, int Quantity, decimal LineTotal);

public record ShiftDto(
    Guid Id,
    Guid BranchId,
    Guid CashierUserId,
    decimal OpeningCash,
    decimal ClosingCashExpected,
    decimal? ClosingCashActual,
    decimal? VarianceAmount,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    string Status,
    bool AutoClosed,
    decimal CashSalesTotal,
    IReadOnlyList<ShiftCashCountDto> CashCounts);

public record VoidSaleRequest(string Reason);
public record VoidRequestDto(
    Guid Id, Guid SaleId, Guid RequestedByUserId, string Reason, Guid? ApprovedByUserId, DateTime CreatedAt);
