namespace POS.Application.Shifts;

public record OpenShiftRequest(Guid BranchId, decimal OpeningCash);
public record CloseShiftRequest(decimal ClosingCashActual);

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
    decimal CashSalesTotal);

public record VoidSaleRequest(string Reason);
public record VoidRequestDto(
    Guid Id, Guid SaleId, Guid RequestedByUserId, string Reason, Guid? ApprovedByUserId, DateTime CreatedAt);
