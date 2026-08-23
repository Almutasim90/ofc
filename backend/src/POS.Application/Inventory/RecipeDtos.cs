namespace POS.Application.Inventory;

public record RecipeLineDto(Guid RawMaterialId, string RawMaterialNameAr, string RawMaterialNameEn, string Unit, decimal QuantityRequired);

public record RecipeLineRequest(Guid RawMaterialId, decimal QuantityRequired);

public record SetRecipeRequest(Guid BranchId, List<RecipeLineRequest> Lines);
