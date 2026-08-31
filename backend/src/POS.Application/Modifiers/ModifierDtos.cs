namespace POS.Application.Modifiers;

public record ModifierOptionDto(Guid Id, string NameAr, string NameEn, decimal PriceDelta, bool IsActive);
public record ModifierGroupDto(Guid Id, string NameAr, string NameEn, int MinSelect, int MaxSelect, bool IsRequired, List<ModifierOptionDto> Options, List<Guid> MenuItemIds);
public record SaveModifierOptionRequest(Guid? Id, string NameAr, string NameEn, decimal PriceDelta, bool IsActive);
public record SaveModifierGroupRequest(string NameAr, string NameEn, int MinSelect, int MaxSelect, bool IsRequired, List<SaveModifierOptionRequest> Options, List<Guid> MenuItemIds);
public record ValidateModifierSelectionRequest(Guid MenuItemId, List<Guid> ModifierOptionIds);
public record ValidatedModifierSelectionDto(decimal PriceDelta, List<ModifierOptionDto> Options);
