namespace POS.Domain.Entities;
public static class RestaurantOrderStatuses { public const string Open="Open", PendingApproval="PendingApproval", Sent="Sent", Paid="Paid", Closed="Closed", Cancelled="Cancelled"; public static readonly IReadOnlySet<string> All=new HashSet<string>{Open,PendingApproval,Sent,Paid,Closed,Cancelled}; }
public class RestaurantOrder
{
    public Guid Id { get; set; } public Guid BranchId { get; set; } public Branch Branch { get; set; }=null!; public int OrderNumber { get; set; }
    public Guid OrderTypeId { get; set; } public OrderType OrderType { get; set; }=null!; public Guid? TableId { get; set; } public RestaurantTable? Table { get; set; }
    public string? CarPlateNumber { get; set; } public Guid? CashierUserId { get; set; } public Guid? CashShiftId { get; set; }
    public DateOnly BusinessDate { get; set; } public DateTime CreatedAt { get; set; } public decimal Subtotal { get; set; } public decimal DiscountAmount { get; set; }
    public decimal GrandTotal { get; set; } public string Status { get; set; }=RestaurantOrderStatuses.Open; public int PaymentRevision { get; set; } public Guid? SalesChannelId { get; set; } public Guid? OrderingSessionId { get; set; }
    public DateTime? SubmittedAt { get; set; } public DateTime? ApprovedAt { get; set; } public Guid? ApprovedByUserId { get; set; } public DateTime? RejectedAt { get; set; } public string? RejectionReason { get; set; }
    public bool? InvoicePricesIncludeTax { get; set; } public decimal? InvoiceDefaultTaxRate { get; set; } public string? InvoiceCurrency { get; set; }
    public string? InvoiceLegalNameAr { get; set; } public string? InvoiceLegalNameEn { get; set; } public string? InvoiceTaxRegistrationNumber { get; set; } public string? InvoiceCommercialRegistrationNumber { get; set; }
    public string? InvoiceAddressAr { get; set; } public string? InvoiceAddressEn { get; set; } public string? InvoicePhone { get; set; } public string? InvoiceFooter { get; set; }
    public DateTime? InvoiceSnapshotCapturedAt { get; set; } public decimal? InvoiceSubtotalSnapshot { get; set; } public decimal? InvoiceDiscountSnapshot { get; set; } public decimal? InvoiceTaxSnapshot { get; set; } public decimal? InvoiceGrandTotalSnapshot { get; set; }
    public ICollection<RestaurantOrderItem> Items { get; set; }=[];
    public ICollection<OrderCancellation> Cancellations { get; set; }=[];
    public ICollection<OrderPayment> Payments { get; set; }=[];
    public ICollection<BillSplit> BillSplits { get; set; }=[];
    public ICollection<OrderEditLog> EditLogs { get; set; }=[];
}
