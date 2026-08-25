namespace POS.Application.Inventory;

public record StockStatusDto(
    Guid RawMaterialId, string NameAr, string NameEn, string Unit,
    decimal CurrentQuantity, decimal LowStockThreshold, bool IsLowStock,
    Guid? SupplyPackageId, string? PackageNameAr, string? PackageNameEn, decimal? BaseQuantityPerPackage);

public record AdjustStockRequest(Guid BranchId, Guid RawMaterialId, decimal QuantityChange, string Reason);

public record SetLowStockThresholdRequest(Guid BranchId, Guid RawMaterialId, decimal Threshold);

public record SupplyPackageDto(Guid Id, Guid RawMaterialId, string NameAr, string NameEn, decimal BaseQuantity, bool IsActive);
public record UpsertSupplyPackageRequest(Guid RawMaterialId, string NameAr, string NameEn, decimal BaseQuantity, bool IsActive = true);
public record ReceiveStockRequest(Guid BranchId, Guid SupplyPackageId, decimal PackageCount, string? Note, DateOnly? ReceivedDate = null);
public record StockReceiptDto(Guid Id, Guid BranchId, Guid RawMaterialId, string RawMaterialNameAr,
    string RawMaterialNameEn, string Unit, Guid SupplyPackageId, string PackageName, decimal PackageCount,
    decimal BaseQuantityAdded, string? Note, DateTime ReceivedAt);
public record CreateInventoryItemRequest(Guid BranchId, string NameAr, string NameEn, string MeasurementType,
    string PackageNameAr, string PackageNameEn, decimal BaseQuantityPerPackage, decimal InitialPackageCount,
    decimal LowStockThreshold, string? Note);
public record CreateInventoryItemResult(Guid RawMaterialId, Guid SupplyPackageId, decimal InitialQuantityAdded);
