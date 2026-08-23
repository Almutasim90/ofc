using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Catalog;

public class ProductService(IAppDbContext db)
{
    public async Task<List<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Products
            .OrderBy(p => p.NameEn)
            .Select(p => new ProductDto(p.Id, p.NameAr, p.NameEn, p.Category, p.Price, p.IconOrImageUrl, p.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Category = request.Category,
            Price = request.Price,
            IconOrImageUrl = request.IconOrImageUrl,
            IsActive = true,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return new ProductDto(product.Id, product.NameAr, product.NameEn, product.Category, product.Price,
            product.IconOrImageUrl, product.IsActive);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Product '{id}' not found.");

        product.NameAr = request.NameAr;
        product.NameEn = request.NameEn;
        product.Category = request.Category;
        product.Price = request.Price;
        product.IconOrImageUrl = request.IconOrImageUrl;
        product.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return new ProductDto(product.Id, product.NameAr, product.NameEn, product.Category, product.Price,
            product.IconOrImageUrl, product.IsActive);
    }
}
