namespace POS.Application.Closing;

public record ClosingScheduleConfigDto(Guid Id, TimeOnly DefaultCloseTime, bool IsActive);
public record UpdateClosingScheduleConfigRequest(TimeOnly DefaultCloseTime, bool IsActive);

public record ClosingScheduleExceptionDto(
    Guid Id, DateOnly Date, TimeOnly OverrideCloseTime, Guid? BranchId, string Reason);
public record UpsertClosingScheduleExceptionRequest(
    DateOnly Date, TimeOnly OverrideCloseTime, Guid? BranchId, string Reason);

public record UpcomingClosingDto(
    DateTime ScheduledCloseAt, int MinutesRemaining, bool Warning, bool ScheduleActive);
