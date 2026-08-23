namespace POS.Application.Catalog;

public record ProductDto(
    Guid Id, string NameAr, string NameEn, string Category, decimal Price, string? IconOrImageUrl, bool IsActive);

public record CreateProductRequest(
    string NameAr, string NameEn, string Category, decimal Price, string? IconOrImageUrl);

public record UpdateProductRequest(
    string NameAr, string NameEn, string Category, decimal Price, string? IconOrImageUrl, bool IsActive);
