namespace POS.Domain.Entities;

public class RawMaterial
{
    public Guid Id { get; set; }
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;

    /// <summary>e.g. "piece", "kg", "liter" - free text unit label.</summary>
    public string Unit { get; set; } = string.Empty;
}
