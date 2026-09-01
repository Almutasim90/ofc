using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Application.CashShifts;

public class CashShiftService(IAppDbContext db, ICurrentUserService user)
{
    public Task<List<CashShiftDto>> GetAsync(Guid branchId, CancellationToken ct = default) => db.CashShifts.Where(x => x.BranchId == branchId).OrderByDescending(x => x.OpenedAt).Take(100).Select(x => new CashShiftDto(x.Id, x.BranchId, x.OpenedByUserId, x.ClosedByUserId, x.OpeningFloat, x.OpenedAt, x.ClosedAt, x.Status, x.ExpectedCash, x.CountedCash, x.VarianceCash)).ToListAsync(ct);

    public async Task<CashShiftDto> OpenAsync(OpenCashShiftRequest request, CancellationToken ct = default)
    {
        RequirePermission();
        if (request.OpeningFloat < 0) throw new ValidationException("Opening float cannot be negative.");
        if (!await db.Branches.AnyAsync(x => x.Id == request.BranchId && x.IsActive, ct)) throw new NotFoundException("Active branch not found.");
        if (await db.CashShifts.AnyAsync(x => x.BranchId == request.BranchId && x.Status == CashShiftStatuses.Open, ct)) throw new ConflictException("This branch already has an open cash shift.");
        var shift = new CashShift { Id = Guid.NewGuid(), BranchId = request.BranchId, OpenedByUserId = user.UserId!.Value, OpeningFloat = request.OpeningFloat, OpenedAt = DateTime.UtcNow };
        db.CashShifts.Add(shift); await db.SaveChangesAsync(ct); return Dto(shift);
    }

    public async Task<CashShiftDto> CloseAsync(Guid id, CloseCashShiftRequest request, CancellationToken ct = default)
    {
        RequirePermission(); ValidateCounts(request.Counts);
        var shift = await db.CashShifts.FirstOrDefaultAsync(x => x.Id == id && x.Status == CashShiftStatuses.Open, ct) ?? throw new NotFoundException("Open cash shift not found.");
        var cash = await db.OrderPayments.Where(x => x.CashShiftId == id && x.PaymentMethod.Code == "CASH").SumAsync(x => (decimal?)x.Amount, ct) ?? 0;
        var counted = request.Counts.Sum(x => x.DenominationValue * x.CountedQty);
        foreach (var count in request.Counts) db.CashCounts.Add(new() { Id = Guid.NewGuid(), CashShiftId = id, DenominationValue = count.DenominationValue, DenominationType = count.DenominationType, CountedQty = count.CountedQty, CreatedAt = DateTime.UtcNow });
        shift.ExpectedCash = shift.OpeningFloat + cash; shift.CountedCash = counted; shift.VarianceCash = counted - shift.ExpectedCash; shift.Status = CashShiftStatuses.Closed; shift.ClosedAt = DateTime.UtcNow; shift.ClosedByUserId = user.UserId;
        await db.SaveChangesAsync(ct); return Dto(shift);
    }

    public static void ValidateCounts(IReadOnlyList<CashDenominationRequest> counts) { if (counts.Count == 0) throw new ValidationException("At least one denomination is required."); if (counts.Any(x => x.DenominationValue <= 0 || x.CountedQty < 0 || (x.DenominationType != "Note" && x.DenominationType != "Coin"))) throw new ValidationException("Invalid cash denomination."); if (counts.Select(x => (x.DenominationValue, x.DenominationType)).Distinct().Count() != counts.Count) throw new ValidationException("Duplicate denomination."); }
    private void RequirePermission() { if (user.UserId is null) throw new UnauthorizedException("Authenticated user is required."); if (!user.Permissions.Contains(PermissionKeys.SalesEdit)) throw new ForbiddenException("Cash shift management permission is required."); }
    private static CashShiftDto Dto(CashShift x) => new(x.Id, x.BranchId, x.OpenedByUserId, x.ClosedByUserId, x.OpeningFloat, x.OpenedAt, x.ClosedAt, x.Status, x.ExpectedCash, x.CountedCash, x.VarianceCash);
}
