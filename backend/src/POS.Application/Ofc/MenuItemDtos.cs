namespace POS.Application.Ofc;

public record MenuItemDto(Guid Id, Guid CategoryId, string NameAr, string NameEn, string Kind, decimal BasePrice, string? ImageUrl, int SortOrder, bool IsActive);

public record CreateMenuItemRequest(Guid CategoryId, string NameAr, string NameEn, string Kind, decimal BasePrice, string? ImageUrl, int SortOrder);

public record UpdateMenuItemRequest(Guid CategoryId, string NameAr, string NameEn, decimal BasePrice, string? ImageUrl, int SortOrder, bool IsActive);

public record ComboComponentOptionDto(Guid MenuItemId, string MenuItemNameAr, string MenuItemNameEn, decimal PriceDelta, bool IsDefault);

public record ComboComponentDto(string SlotLabel, bool IsRequired, int MinSelect, int MaxSelect, List<ComboComponentOptionDto> Options);

public record ComboComponentOptionInput(Guid MenuItemId, decimal PriceDelta, bool IsDefault);

public record ComboComponentInput(string SlotLabel, bool IsRequired, int MinSelect, int MaxSelect, List<ComboComponentOptionInput> Options);

public record SetComboComponentsRequest(List<ComboComponentInput> Components);
