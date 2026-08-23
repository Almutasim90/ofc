namespace POS.Domain.Entities;

public class ClosingScheduleException
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly OverrideCloseTime { get; set; }
    public Guid? BranchId { get; set; }
    public string Reason { get; set; } = string.Empty;
}
