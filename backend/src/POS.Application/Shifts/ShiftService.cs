using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Application.Shifts;

public class ShiftService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<ShiftDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var shift = await db.Shifts
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CashierUserId == userId && s.Status == ShiftStatus.Open, cancellationToken);
        return shift is null ? null : await ToDtoAsync(shift, cancellationToken);
    }

    public async Task<ShiftDto?> GetLatestClosedAsync(CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        var shift = await db.Shifts.AsNoTracking()
            .Where(s => s.CashierUserId == userId && s.Status == ShiftStatus.Closed)
            .OrderByDescending(s => s.ClosedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return shift is null ? null : await ToDtoAsync(shift, cancellationToken);
    }

    public async Task<ShiftDto> OpenAsync(OpenShiftRequest request, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        EnsureBranchScope(request.BranchId);
        if (request.OpeningCash < 0)
            throw new ValidationException("Opening cash cannot be negative.");

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId && b.IsActive, cancellationToken);
        if (branch is null)
            throw new ValidationException("The selected branch is unavailable.");

        var alreadyOpen = await db.Shifts.AnyAsync(
            s => s.CashierUserId == userId && s.Status == ShiftStatus.Open, cancellationToken);
        if (alreadyOpen)
            throw new ValidationException("This cashier already has an open shift.");

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            BranchId = request.BranchId,
            CashierUserId = userId,
            OpeningCash = request.OpeningCash ?? branch.DefaultOpeningFloat,
            OpenedAt = DateTime.UtcNow,
        };
        db.Shifts.Add(shift);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { throw new ValidationException("This cashier already has an open shift."); }
        return await ToDtoAsync(shift, cancellationToken);
    }

    public async Task<ShiftDto> CloseAsync(Guid shiftId, CloseShiftRequest request, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        if (request.Counts is null || request.Counts.Count == 0)
            throw new ValidationException("At least one cash denomination count is required.");
        if (request.Counts.Any(c => c.Denomination <= 0 || c.Quantity < 0))
            throw new ValidationException("Cash denominations must be positive and quantities cannot be negative.");
        if (request.Counts.GroupBy(c => c.Denomination).Any(g => g.Count() > 1))
            throw new ValidationException("Each cash denomination may appear only once.");

        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
            ?? throw new NotFoundException("Shift not found.");
        if (shift.CashierUserId != userId)
            throw new ValidationException("Only the cashier who opened this shift can close it.");
        if (shift.Status != ShiftStatus.Open)
            throw new ValidationException("This shift is already closed.");

        var cashSales = await db.Sales
            .Where(s => s.ShiftId == shift.Id && s.Status == SaleStatus.Completed && s.PaymentMethod == PaymentMethods.Cash)
            .Where(s => s.Channel.IsInStore)
            .SumAsync(s => s.TotalAmount, cancellationToken);
        shift.ClosingCashExpected = ShiftCashCalculator.Expected(shift.OpeningCash, cashSales);
        var closingCashActual = ShiftCashCalculator.Actual(request.Counts);
        shift.ClosingCashActual = closingCashActual;
        shift.VarianceAmount = ShiftCashCalculator.Variance(closingCashActual, shift.ClosingCashExpected);
        shift.ClosedAt = DateTime.UtcNow;
        shift.Status = ShiftStatus.Closed;
        shift.AutoClosed = false;

        foreach (var count in request.Counts.Where(c => c.Quantity > 0))
        {
            db.ShiftCashCounts.Add(new ShiftCashCount
            {
                Id = Guid.NewGuid(), ShiftId = shift.Id, CountType = "Closing",
                Denomination = count.Denomination, Quantity = count.Quantity,
            });
        }

        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            throw new ValidationException("This shift was closed from another session. Refresh and try again.");
        }
        var countDtos = request.Counts.Where(c => c.Quantity > 0)
            .OrderByDescending(c => c.Denomination)
            .Select(c => new ShiftCashCountDto(c.Denomination, c.Quantity, c.Denomination * c.Quantity))
            .ToList();
        return ToDto(shift, cashSales, countDtos);
    }

    private async Task<ShiftDto> ToDtoAsync(Shift shift, CancellationToken cancellationToken)
    {
        var cashSales = await db.Sales
            .Where(s => s.ShiftId == shift.Id && s.Status == SaleStatus.Completed && s.PaymentMethod == PaymentMethods.Cash)
            .Where(s => s.Channel.IsInStore)
            .SumAsync(s => s.TotalAmount, cancellationToken);
        return ToDto(shift, cashSales, await GetCashCountsAsync(shift.Id, cancellationToken));
    }

    private async Task<IReadOnlyList<ShiftCashCountDto>> GetCashCountsAsync(Guid shiftId, CancellationToken cancellationToken) =>
        await db.ShiftCashCounts.AsNoTracking().Where(c => c.ShiftId == shiftId && c.CountType == "Closing")
            .OrderByDescending(c => c.Denomination)
            .Select(c => new ShiftCashCountDto(c.Denomination, c.Quantity, c.Denomination * c.Quantity))
            .ToListAsync(cancellationToken);

    private static ShiftDto ToDto(Shift shift, decimal cashSales, IReadOnlyList<ShiftCashCountDto>? counts = null) => new(
        shift.Id, shift.BranchId, shift.CashierUserId, shift.OpeningCash,
        shift.Status == ShiftStatus.Open ? ShiftCashCalculator.Expected(shift.OpeningCash, cashSales) : shift.ClosingCashExpected,
        shift.ClosingCashActual, shift.VarianceAmount, shift.OpenedAt, shift.ClosedAt,
        shift.Status, shift.AutoClosed, cashSales, counts ?? []);

    private Guid RequireUserId() => currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");

    private void EnsureBranchScope(Guid branchId)
    {
        if (!currentUser.BypassBranchFilter && branchId != currentUser.BranchId)
            throw new ValidationException("You do not have access to this branch.");
    }
}
