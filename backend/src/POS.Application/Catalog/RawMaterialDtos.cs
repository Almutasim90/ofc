namespace POS.Application.Catalog;

public record RawMaterialDto(Guid Id, string NameAr, string NameEn, string Unit);

public record CreateRawMaterialRequest(string NameAr, string NameEn, string Unit);

public record UpdateRawMaterialRequest(string NameAr, string NameEn, string Unit);
