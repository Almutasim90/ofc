using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using POS.Application.Closing;
using POS.Domain.Constants;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Services;

public class AutomaticShiftClosingService(IServiceScopeFactory scopeFactory, ILogger<AutomaticShiftClosingService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try { await CloseDueShiftsAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Automatic shift closing check failed."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task CloseDueShiftsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = await db.ClosingScheduleConfigs.IgnoreQueryFilters().AsNoTracking().SingleAsync(cancellationToken);
        if (!config.IsActive) return;

        var shifts = await db.Shifts.IgnoreQueryFilters()
            .Where(s => s.Status == ShiftStatus.Open).ToListAsync(cancellationToken);
        if (shifts.Count == 0) return;

        var earliest = shifts.Min(s => DateOnly.FromDateTime(MuscatClock.ToLocal(s.OpenedAt)));
        var latest = shifts.Max(s => DateOnly.FromDateTime(MuscatClock.ToLocal(s.OpenedAt)));
        var exceptions = await db.ClosingScheduleExceptions.IgnoreQueryFilters().AsNoTracking()
            .Where(e => e.Date >= earliest && e.Date <= latest.AddDays(7)).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var shift in shifts)
        {
            var due = ClosingScheduleCalculator.GetDueUtc(shift.OpenedAt, config, exceptions, shift.BranchId);
            if (due is null || now < due.Value) continue;
            var cashSales = await db.Sales.IgnoreQueryFilters()
                .Where(s => s.ShiftId == shift.Id && s.Status == SaleStatus.Completed && s.Channel.IsInStore)
                .SumAsync(s => s.CashAmount ?? (s.PaymentMethod == PaymentMethods.Cash ? s.TotalAmount : 0m), cancellationToken);
            shift.ClosingCashExpected = POS.Application.Shifts.ShiftCashCalculator.Expected(shift.OpeningCash, cashSales);
            shift.ClosingCashActual = null;
            shift.VarianceAmount = null;
            shift.ClosedAt = now;
            shift.Status = ShiftStatus.Closed;
            shift.AutoClosed = true;
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}
