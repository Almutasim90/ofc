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
public record SalesTrendPointDto(DateOnly Date, decimal TotalSales, int InvoiceCount, decimal ItemsSold, decimal CashSales, decimal CardSales);
public record ProductSalesSummaryDto(
    Guid ProductId, string NameAr, string NameEn, decimal QuantitySold, decimal TotalSales, int InvoiceCount,
    decimal CashQuantitySold, decimal CashTotalSales, int CashInvoiceCount,
    decimal CardQuantitySold, decimal CardTotalSales, int CardInvoiceCount);
public record ManagerDashboardDto(
    DateOnly From, DateOnly To, decimal TotalSales, decimal TotalDiscounts, int InvoiceCount, decimal ItemsSold, decimal AverageTicket,
    List<SalesTrendPointDto> DailyTrend, List<BranchSalesSummaryDto> Branches,
    List<PaymentBreakdownDto> PaymentBreakdown, List<ProductSalesSummaryDto> Products, List<ShiftVariancePointDto> ShiftVariances);
public record DiscountSaleDto(Guid SaleId, Guid BranchId, Guid CashierUserId, DateTime CreatedAt,
    decimal DiscountAmount, decimal TotalAmount);
public record DiscountReportDto(DateOnly From, DateOnly To, decimal TotalDiscounts, List<DiscountSaleDto> Sales);
public record ChannelSalesDto(Guid ChannelId, string NameAr, string NameEn, decimal TotalSales, int InvoiceCount);
public record ShiftVariancePointDto(Guid ShiftId, DateTime OpenedAt, decimal VarianceAmount);
