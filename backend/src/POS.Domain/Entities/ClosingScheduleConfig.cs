namespace POS.Domain.Entities;

public class ClosingScheduleConfig
{
    public Guid Id { get; set; }
    public TimeOnly DefaultCloseTime { get; set; } = new(23, 45);
    public bool IsActive { get; set; } = true;
}
