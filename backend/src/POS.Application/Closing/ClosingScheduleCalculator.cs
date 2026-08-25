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

        // The schedule is attached to the shift's Muscat business date. A close
        // time earlier than the opening time (for example 01:00) means the next
        // calendar day, not that the exception should be skipped.
        var businessDate = DateOnly.FromDateTime(openedLocal);
        byDate.TryGetValue(businessDate, out var candidates);
        var exception = candidates?.FirstOrDefault(e => e.BranchId == branchId)
            ?? candidates?.FirstOrDefault(e => e.BranchId is null);
        var closeTime = exception?.OverrideCloseTime ?? config.DefaultCloseTime;
        var dueLocal = businessDate.ToDateTime(closeTime);
        if (dueLocal <= openedLocal) dueLocal = dueLocal.AddDays(1);
        return MuscatClock.ToUtc(dueLocal);
    }
}
