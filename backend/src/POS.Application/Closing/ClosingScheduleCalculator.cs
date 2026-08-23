using POS.Domain.Entities;

namespace POS.Application.Closing;

public static class ClosingScheduleCalculator
{
    public static DateTime? GetDueUtc(
        DateTime shiftOpenedUtc,
        ClosingScheduleConfig config,
        IEnumerable<ClosingScheduleException> exceptions,
        Guid branchId)
    {
        if (!config.IsActive) return null;
        var openedLocal = MuscatClock.ToLocal(shiftOpenedUtc);
        var byDate = exceptions.GroupBy(e => e.Date).ToDictionary(g => g.Key, g => g.ToList());

        for (var offset = 0; offset <= 7; offset++)
        {
            var date = DateOnly.FromDateTime(openedLocal).AddDays(offset);
            byDate.TryGetValue(date, out var candidates);
            var exception = candidates?.FirstOrDefault(e => e.BranchId == branchId)
                ?? candidates?.FirstOrDefault(e => e.BranchId is null);
            var closeTime = exception?.OverrideCloseTime ?? config.DefaultCloseTime;
            var dueLocal = date.ToDateTime(closeTime);
            if (dueLocal > openedLocal)
                return MuscatClock.ToUtc(dueLocal);
        }
        return null;
    }
}
