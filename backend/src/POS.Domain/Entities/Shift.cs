using POS.Domain.Constants;

namespace POS.Domain.Entities;

public class Shift
{
    public Guid Id { get; set; }
    public Guid BranchId { get; set; }
    public Guid CashierUserId { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal ClosingCashExpected { get; set; }
    public decimal? ClosingCashActual { get; set; }
    public decimal? VarianceAmount { get; set; }
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string Status { get; set; } = ShiftStatus.Open;
    public bool AutoClosed { get; set; }
    public uint Version { get; set; }

    public ICollection<Sale> Sales { get; set; } = new List<Sale>();
    public ICollection<ShiftCashCount> CashCounts { get; set; } = new List<ShiftCashCount>();
}
