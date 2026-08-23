namespace POS.Application.Inventory;

public record StockStatusDto(
    Guid RawMaterialId, string NameAr, string NameEn, string Unit,
    decimal CurrentQuantity, decimal LowStockThreshold, bool IsLowStock);

public record AdjustStockRequest(Guid BranchId, Guid RawMaterialId, decimal QuantityChange, string Reason);

public record SetLowStockThresholdRequest(Guid BranchId, Guid RawMaterialId, decimal Threshold);
