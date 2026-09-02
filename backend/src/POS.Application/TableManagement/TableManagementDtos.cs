namespace POS.Application.TableManagement;

public record RestaurantFloorDto(Guid Id, Guid BranchId, string Name, int SortOrder, bool IsActive);
public record SaveRestaurantFloorRequest(Guid BranchId, string Name, int SortOrder, bool IsActive = true);
public record TableLayoutDto(Guid Id, Guid BranchId, string Label, int? Capacity, bool IsActive,
    Guid? FloorId, string? FloorName, int PositionX, int PositionY, string Shape);
public record SaveTableLayoutRequest(Guid BranchId, string Label, int? Capacity, Guid? FloorId,
    int PositionX, int PositionY, string Shape, bool IsActive = true);
public record TableOrderStatusDto(Guid Id, int OrderNumber, string Status, string OrderTypeCode,
    decimal GrandTotal, DateTime CreatedAt);
public record TableStatusDto(Guid Id, Guid BranchId, string Label, int? Capacity, bool IsActive,
    Guid? FloorId, string? FloorName, int PositionX, int PositionY, string Shape, bool IsOccupied,
    Guid? OpenQrSessionId, DateTime? QrSessionOpenedAt, IReadOnlyList<TableOrderStatusDto> Orders);
