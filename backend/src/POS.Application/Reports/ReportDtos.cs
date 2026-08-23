namespace POS.Application.Reports;

public record PaymentBreakdownDto(string PaymentMethod, decimal TotalAmount, int InvoiceCount);
public record DailySalesReportDto(
    Guid BranchId, string BranchNameAr, string BranchNameEn, DateOnly BusinessDate,
    decimal TotalSales, int InvoiceCount, List<PaymentBreakdownDto> PaymentBreakdown);
public record BranchSalesSummaryDto(
    Guid BranchId, string BranchNameAr, string BranchNameEn, decimal TotalSales, int InvoiceCount);
public record GlobalSalesReportDto(
    DateOnly BusinessDate, decimal TotalSales, int InvoiceCount, List<BranchSalesSummaryDto> Branches);
public record InventoryConsumptionDto(
    Guid RawMaterialId, string NameAr, string NameEn, string Unit, decimal QuantityConsumed);
public record ShiftInventoryReportDto(Guid ShiftId, Guid BranchId, List<InventoryConsumptionDto> Materials);
