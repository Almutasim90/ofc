using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Application.Closing;

public class ClosingScheduleService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<ClosingScheduleConfigDto> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        var config = await db.ClosingScheduleConfigs.SingleAsync(cancellationToken);
        return new(config.Id, config.DefaultCloseTime, config.IsActive);
    }

    public async Task<ClosingScheduleConfigDto> UpdateConfigAsync(
        UpdateClosingScheduleConfigRequest request, CancellationToken cancellationToken = default)
    {
        EnsureGeneralManager();
        var config = await db.ClosingScheduleConfigs.SingleAsync(cancellationToken);
        config.DefaultCloseTime = request.DefaultCloseTime;
        config.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return new(config.Id, config.DefaultCloseTime, config.IsActive);
    }

    public async Task<List<ClosingScheduleExceptionDto>> GetExceptionsAsync(CancellationToken cancellationToken = default) =>
        await db.ClosingScheduleExceptions.OrderBy(e => e.Date).ThenBy(e => e.BranchId)
            .Select(e => new ClosingScheduleExceptionDto(e.Id, e.Date, e.OverrideCloseTime, e.BranchId, e.Reason))
            .ToListAsync(cancellationToken);

    public async Task<ClosingScheduleExceptionDto> CreateExceptionAsync(
        UpsertClosingScheduleExceptionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureGeneralManager();
        ValidateException(request);
        var duplicate = await db.ClosingScheduleExceptions.AnyAsync(
            e => e.Date == request.Date && e.BranchId == request.BranchId, cancellationToken);
        if (duplicate) throw new ValidationException("A closing exception already exists for this date and branch scope.");
        var entity = new ClosingScheduleException
        {
            Id = Guid.NewGuid(), Date = request.Date, OverrideCloseTime = request.OverrideCloseTime,
            BranchId = request.BranchId, Reason = request.Reason.Trim(),
        };
        db.ClosingScheduleExceptions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<ClosingScheduleExceptionDto> UpdateExceptionAsync(
        Guid id, UpsertClosingScheduleExceptionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureGeneralManager();
        ValidateException(request);
        var entity = await db.ClosingScheduleExceptions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Closing exception not found.");
        var duplicate = await db.ClosingScheduleExceptions.AnyAsync(
            e => e.Id != id && e.Date == request.Date && e.BranchId == request.BranchId, cancellationToken);
        if (duplicate) throw new ValidationException("A closing exception already exists for this date and branch scope.");
        entity.Date = request.Date;
        entity.OverrideCloseTime = request.OverrideCloseTime;
        entity.BranchId = request.BranchId;
        entity.Reason = request.Reason.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task DeleteExceptionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureGeneralManager();
        var entity = await db.ClosingScheduleExceptions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new NotFoundException("Closing exception not found.");
        db.ClosingScheduleExceptions.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UpcomingClosingDto> GetUpcomingAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");
        var shift = await db.Shifts.AsNoTracking().FirstOrDefaultAsync(
            s => s.CashierUserId == userId && s.Status == ShiftStatus.Open, cancellationToken);
        var config = await db.ClosingScheduleConfigs.AsNoTracking().SingleAsync(cancellationToken);
        if (shift is null || !config.IsActive)
            return new(DateTime.MinValue, 0, false, config.IsActive);
        var openedDate = DateOnly.FromDateTime(MuscatClock.ToLocal(shift.OpenedAt));
        var exceptions = await db.ClosingScheduleExceptions.AsNoTracking()
            .Where(e => e.Date >= openedDate && e.Date <= openedDate.AddDays(7)).ToListAsync(cancellationToken);
        var due = ClosingScheduleCalculator.GetDueUtc(shift.OpenedAt, config, exceptions, shift.BranchId)
            ?? DateTime.MinValue;
        var minutes = Math.Max(0, (int)Math.Ceiling((due - DateTime.UtcNow).TotalMinutes));
        return new(due, minutes, due != DateTime.MinValue && minutes <= 30, true);
    }

    private void EnsureGeneralManager()
    {
        if (currentUser.RoleName != RoleNames.GeneralManager)
            throw new UnauthorizedException("Only the General Manager can configure closing schedules.");
    }
    private static void ValidateException(UpsertClosingScheduleExceptionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason)) throw new ValidationException("A reason is required.");
    }
    private static ClosingScheduleExceptionDto ToDto(ClosingScheduleException e) =>
        new(e.Id, e.Date, e.OverrideCloseTime, e.BranchId, e.Reason);
}
