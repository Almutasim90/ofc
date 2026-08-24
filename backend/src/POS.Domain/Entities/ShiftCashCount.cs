namespace POS.Domain.Entities;

public class ShiftCashCount
{
    public Guid Id { get; set; }
    public Guid ShiftId { get; set; }
    public string CountType { get; set; } = "Closing";
    public decimal Denomination { get; set; }
    public int Quantity { get; set; }

    public Shift Shift { get; set; } = null!;
}
