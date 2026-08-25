using POS.Application.Closing;
using POS.Application.Shifts;
using POS.Domain.Entities;
using Xunit;

namespace POS.Application.Tests;

public class ShiftClosingTests
{
    [Fact]
    public void Default_schedule_closes_same_Muscat_day_when_opened_before_close()
    {
        var config = new ClosingScheduleConfig { IsActive = true, DefaultCloseTime = new TimeOnly(23, 45) };
        var openedUtc = MuscatClock.ToUtc(new DateTime(2026, 8, 25, 8, 0, 0));
        var due = ClosingScheduleCalculator.GetDueUtc(openedUtc, config, [], Guid.NewGuid());
        Assert.Equal(MuscatClock.ToUtc(new DateTime(2026, 8, 25, 23, 45, 0)), due);
    }

    [Fact]
    public void Shift_opened_after_close_rolls_to_next_business_close()
    {
        var config = new ClosingScheduleConfig { IsActive = true, DefaultCloseTime = new TimeOnly(23, 45) };
        var openedUtc = MuscatClock.ToUtc(new DateTime(2026, 8, 25, 23, 50, 0));
        var due = ClosingScheduleCalculator.GetDueUtc(openedUtc, config, [], Guid.NewGuid());
        Assert.Equal(MuscatClock.ToUtc(new DateTime(2026, 8, 26, 23, 45, 0)), due);
    }

    [Fact]
    public void Branch_exception_overrides_global_exception()
    {
        var branchId = Guid.NewGuid();
        var config = new ClosingScheduleConfig { IsActive = true, DefaultCloseTime = new TimeOnly(23, 45) };
        var date = new DateOnly(2026, 8, 25);
        var exceptions = new[] {
            new ClosingScheduleException { Date = date, BranchId = null, OverrideCloseTime = new TimeOnly(1, 0), Reason = "global" },
            new ClosingScheduleException { Date = date, BranchId = branchId, OverrideCloseTime = new TimeOnly(22, 0), Reason = "branch" },
        };
        var openedUtc = MuscatClock.ToUtc(new DateTime(2026, 8, 25, 8, 0, 0));
        Assert.Equal(MuscatClock.ToUtc(new DateTime(2026, 8, 25, 22, 0, 0)), ClosingScheduleCalculator.GetDueUtc(openedUtc, config, exceptions, branchId));
    }

    [Fact]
    public void One_am_exception_means_one_am_after_the_business_day()
    {
        var branchId = Guid.NewGuid();
        var date = new DateOnly(2026, 8, 25);
        var config = new ClosingScheduleConfig { IsActive = true, DefaultCloseTime = new TimeOnly(23, 45) };
        var exceptions = new[] { new ClosingScheduleException { Date = date, BranchId = branchId, OverrideCloseTime = new TimeOnly(1, 0), Reason = "holiday" } };
        var openedUtc = MuscatClock.ToUtc(new DateTime(2026, 8, 25, 8, 0, 0));
        Assert.Equal(MuscatClock.ToUtc(new DateTime(2026, 8, 26, 1, 0, 0)), ClosingScheduleCalculator.GetDueUtc(openedUtc, config, exceptions, branchId));
    }

    [Fact]
    public void Closing_cash_and_variance_match_acceptance_example()
    {
        var expected = ShiftCashCalculator.Expected(50m, 120m);
        var actual = ShiftCashCalculator.Actual([new(50m, 3), new(10m, 1), new(5m, 1), new(1m, 3)]);
        Assert.Equal(170m, expected);
        Assert.Equal(168m, actual);
        Assert.Equal(-2m, ShiftCashCalculator.Variance(actual, expected));
    }
}
