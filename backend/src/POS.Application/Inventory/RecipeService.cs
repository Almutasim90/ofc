using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Inventory;

/// <summary>
/// A product's recipe is entirely optional (see PRODUCT_RECIPE_OPTIONAL in the spec):
/// zero lines for a (product, branch) pair means it sells with no stock deduction at all.
/// </summary>
public class RecipeService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<List<RecipeLineDto>> GetAsync(Guid productId, Guid branchId, CancellationToken cancellationToken = default)
    {
        return await db.ProductRecipes
            .Where(r => r.ProductId == productId && r.BranchId == branchId)
            .OrderBy(r => r.RawMaterial.NameEn)
            .Select(r => new RecipeLineDto(
                r.RawMaterialId, r.RawMaterial.NameAr, r.RawMaterial.NameEn, r.RawMaterial.Unit, r.QuantityRequired))
            .ToListAsync(cancellationToken);
    }

    public async Task SetAsync(Guid productId, SetRecipeRequest request, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(request.BranchId);

        var productExists = await db.Products.AnyAsync(p => p.Id == productId, cancellationToken);
        if (!productExists)
        {
            throw new NotFoundException($"Product '{productId}' not found.");
        }

        var existing = await db.ProductRecipes
            .Where(r => r.ProductId == productId && r.BranchId == request.BranchId)
            .ToListAsync(cancellationToken);
        db.ProductRecipes.RemoveRange(existing);

        foreach (var line in request.Lines.Where(l => l.QuantityRequired > 0))
        {
            db.ProductRecipes.Add(new ProductRecipe
            {
                ProductId = productId,
                BranchId = request.BranchId,
                RawMaterialId = line.RawMaterialId,
                QuantityRequired = line.QuantityRequired,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private void EnsureBranchScope(Guid branchId)
    {
        if (!currentUser.BypassBranchFilter && branchId != currentUser.BranchId)
        {
            throw new ValidationException("You do not have access to manage this branch's data.");
        }
    }
}
