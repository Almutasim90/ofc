namespace POS.Domain.Entities;
public class RestaurantOrderItem
{
    public Guid Id { get; set; } public Guid OrderId { get; set; } public RestaurantOrder Order { get; set; }=null!; public Guid MenuItemId { get; set; } public MenuItem MenuItem { get; set; }=null!;
    public string MenuItemNameSnapshot { get; set; }=string.Empty; public decimal UnitPriceSnapshot { get; set; } public int Quantity { get; set; } public decimal LineTotal { get; set; } public string? Notes { get; set; } public bool IsCancelled { get; set; }
    public decimal? InvoiceTaxRateSnapshot { get; set; } public decimal? InvoiceNetSnapshot { get; set; } public decimal? InvoiceTaxSnapshot { get; set; } public decimal? InvoiceGrossSnapshot { get; set; }
    public ICollection<OrderItemComboSelection> ComboSelections { get; set; }=[]; public ICollection<OrderItemModifier> Modifiers { get; set; }=[];
}
