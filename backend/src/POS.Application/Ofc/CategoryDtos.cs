namespace POS.Application.Ofc;

public record CategoryDto(Guid Id, string NameAr, string NameEn, int SortOrder, bool IsActive);

public record CreateCategoryRequest(string NameAr, string NameEn, int SortOrder);

public record UpdateCategoryRequest(string NameAr, string NameEn, int SortOrder, bool IsActive);

public record CategoryBranchAvailabilityDto(Guid BranchId, string BranchNameAr, string BranchNameEn, bool IsAvailable);

public record SetCategoryBranchAvailabilityRequest(Guid BranchId, bool IsAvailable);
