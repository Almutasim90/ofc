namespace POS.Application.RestaurantCatalog;

public record RestaurantTableDto(Guid Id, Guid BranchId, string Label, int? Capacity, bool IsActive);
public record SaveRestaurantTableRequest(Guid BranchId, string Label, int? Capacity, bool IsActive = true);
public record BranchFeatureFlagDto(Guid Id, Guid BranchId, string FeatureKey, bool IsEnabled);
public record SetFeatureFlagRequest(bool IsEnabled);

public record MenuCategoryDto(Guid Id, string NameAr, string NameEn, int SortOrder, bool IsActive, bool IsAvailable);
public record SaveMenuCategoryRequest(string NameAr, string NameEn, int SortOrder, bool IsActive = true);
public record ReorderCategoriesRequest(IReadOnlyList<Guid> CategoryIds);
public record SetCategoryAvailabilityRequest(bool IsAvailable);

public record MenuItemDto(Guid Id, Guid CategoryId, string NameAr, string NameEn, string Kind, decimal BasePrice,
    string? ImageUrl, int SortOrder, bool IsActive);
public record SaveMenuItemRequest(Guid CategoryId, string NameAr, string NameEn, string Kind, decimal BasePrice,
    string? ImageUrl, int SortOrder, bool IsActive = true);

public record ComboOptionDto(Guid Id, Guid MenuItemId, string MenuItemNameAr, string MenuItemNameEn,
    decimal PriceDelta, bool IsDefault);
public record ComboComponentDto(Guid Id, string SlotLabel, bool IsRequired, int MinSelect, int MaxSelect,
    int SortOrder, IReadOnlyList<ComboOptionDto> Options);
public record SaveComboOptionRequest(Guid MenuItemId, decimal PriceDelta, bool IsDefault);
public record SaveComboComponentRequest(string SlotLabel, bool IsRequired, int MinSelect, int MaxSelect,
    int SortOrder, IReadOnlyList<SaveComboOptionRequest> Options);
public record SaveComboDefinitionRequest(IReadOnlyList<SaveComboComponentRequest> Components);
