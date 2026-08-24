namespace POS.Application.Catalog;

public record BranchDto(Guid Id, string NameAr, string NameEn, string Code, decimal DefaultOpeningFloat, bool IsActive);

public record CreateBranchRequest(string NameAr, string NameEn, string Code, decimal DefaultOpeningFloat);

public record UpdateBranchRequest(string NameAr, string NameEn, string Code, decimal DefaultOpeningFloat, bool IsActive);
