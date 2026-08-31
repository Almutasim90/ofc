namespace POS.Application.CashShifts;
public record CashShiftDto(Guid Id,Guid BranchId,Guid OpenedByUserId,Guid? ClosedByUserId,decimal OpeningFloat,DateTime OpenedAt,DateTime? ClosedAt,string Status,decimal? ExpectedCash,decimal? CountedCash,decimal? VarianceCash);
public record OpenCashShiftRequest(Guid BranchId,decimal OpeningFloat);
public record CashDenominationRequest(decimal DenominationValue,string DenominationType,int CountedQty);
public record CloseCashShiftRequest(IReadOnlyList<CashDenominationRequest> Counts);
