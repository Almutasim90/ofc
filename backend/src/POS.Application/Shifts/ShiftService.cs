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

        var branchExists = await db.Branches.AnyAsync(b => b.Id == request.BranchId && b.IsActive, cancellationToken);
        if (!branchExists)
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
            OpeningCash = request.OpeningCash,
            OpenedAt = DateTime.UtcNow,
        };
        db.Shifts.Add(shift);
        await db.SaveChangesAsync(cancellationToken);
        return await ToDtoAsync(shift, cancellationToken);
    }

    public async Task<ShiftDto> CloseAsync(Guid shiftId, CloseShiftRequest request, CancellationToken cancellationToken = default)
    {
        var userId = RequireUserId();
        if (request.ClosingCashActual < 0)
            throw new ValidationException("Actual closing cash cannot be negative.");

        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, cancellationToken)
            ?? throw new NotFoundException("Shift not found.");
        if (shift.CashierUserId != userId)
            throw new ValidationException("Only the cashier who opened this shift can close it.");
        if (shift.Status != ShiftStatus.Open)
            throw new ValidationException("This shift is already closed.");

        var cashSales = await db.Sales
            .Where(s => s.ShiftId == shift.Id && s.Status == SaleStatus.Completed && s.PaymentMethod == PaymentMethods.Cash)
            .SumAsync(s => s.TotalAmount, cancellationToken);
        shift.ClosingCashExpected = shift.OpeningCash + cashSales;
        shift.ClosingCashActual = request.ClosingCashActual;
        shift.VarianceAmount = request.ClosingCashActual - shift.ClosingCashExpected;
        shift.ClosedAt = DateTime.UtcNow;
        shift.Status = ShiftStatus.Closed;
        shift.AutoClosed = false;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(shift, cashSales);
    }

    private async Task<ShiftDto> ToDtoAsync(Shift shift, CancellationToken cancellationToken)
    {
        var cashSales = await db.Sales
            .Where(s => s.ShiftId == shift.Id && s.Status == SaleStatus.Completed && s.PaymentMethod == PaymentMethods.Cash)
            .SumAsync(s => s.TotalAmount, cancellationToken);
        return ToDto(shift, cashSales);
    }

    private static ShiftDto ToDto(Shift shift, decimal cashSales) => new(
        shift.Id, shift.BranchId, shift.CashierUserId, shift.OpeningCash,
        shift.Status == ShiftStatus.Open ? shift.OpeningCash + cashSales : shift.ClosingCashExpected,
        shift.ClosingCashActual, shift.VarianceAmount, shift.OpenedAt, shift.ClosedAt,
        shift.Status, shift.AutoClosed, cashSales);

    private Guid RequireUserId() => currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");

    private void EnsureBranchScope(Guid branchId)
    {
        if (!currentUser.BypassBranchFilter && branchId != currentUser.BranchId)
            throw new ValidationException("You do not have access to this branch.");
    }
}
