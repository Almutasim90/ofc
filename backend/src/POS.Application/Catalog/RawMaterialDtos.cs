namespace POS.Application.Catalog;

public record RawMaterialDto(Guid Id, string NameAr, string NameEn, string Unit, string MeasurementType);

public record CreateRawMaterialRequest(string NameAr, string NameEn, string Unit, string MeasurementType = "Custom");

public record UpdateRawMaterialRequest(string NameAr, string NameEn, string Unit, string MeasurementType = "Custom");
