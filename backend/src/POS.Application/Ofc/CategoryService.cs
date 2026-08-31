using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Ofc;

public class CategoryService(IAppDbContext db, ICurrentUserService currentUser)
{
    /// <summary>All categories, for the admin management screen - not filtered by branch
    /// availability (that's a per-branch toggle layered on top, see GetAvailableForBranchAsync).</summary>
    public async Task<List<CategoryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Categories
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(c.Id, c.NameAr, c.NameEn, c.SortOrder, c.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Implements OFC-Development-Brief.md section 4.1 (GetAvailableCategoriesForBranch):
    /// fail-open, a category with no availability row for this branch is available by default.</summary>
    public async Task<List<CategoryDto>> GetAvailableForBranchAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var unavailable = await db.CategoryBranchAvailabilities
            .IgnoreQueryFilters()
            .Where(a => a.BranchId == branchId && !a.IsAvailable)
            .Select(a => a.CategoryId)
            .ToListAsync(cancellationToken);

        return await db.Categories
            .Where(c => c.IsActive && !unavailable.Contains(c.Id))
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(c.Id, c.NameAr, c.NameEn, c.SortOrder, c.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            SortOrder = request.SortOrder,
            IsActive = true,
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.NameAr, category.NameEn, category.SortOrder, category.IsActive);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Category '{id}' not found.");

        category.NameAr = request.NameAr;
        category.NameEn = request.NameEn;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return new CategoryDto(category.Id, category.NameAr, category.NameEn, category.SortOrder, category.IsActive);
    }

    /// <summary>Per-branch on/off toggle for a category (OFC-System-Detailed-Spec.md section 1.1).
    /// GeneralManager may target any branch; a branch-scoped user may only target their own.</summary>
    public async Task<List<CategoryBranchAvailabilityDto>> GetBranchAvailabilityAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
            throw new NotFoundException($"Category '{categoryId}' not found.");

        var overrides = await db.CategoryBranchAvailabilities
            .Where(a => a.CategoryId == categoryId)
            .ToDictionaryAsync(a => a.BranchId, a => a.IsAvailable, cancellationToken);

        var branches = await db.Branches
            .OrderBy(b => b.NameEn)
            .Select(b => new { b.Id, b.NameAr, b.NameEn })
            .ToListAsync(cancellationToken);

        return branches
            .Select(b => new CategoryBranchAvailabilityDto(b.Id, b.NameAr, b.NameEn, overrides.GetValueOrDefault(b.Id, true)))
            .ToList();
    }

    public async Task SetBranchAvailabilityAsync(Guid categoryId, SetCategoryBranchAvailabilityRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.BypassBranchFilter && request.BranchId != currentUser.BranchId)
            throw new ValidationException("You do not have access to manage this branch's data.");
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
            throw new NotFoundException($"Category '{categoryId}' not found.");

        var availability = await db.CategoryBranchAvailabilities
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.CategoryId == categoryId && a.BranchId == request.BranchId, cancellationToken);

        if (availability is null)
        {
            db.CategoryBranchAvailabilities.Add(new CategoryBranchAvailability
            {
                Id = Guid.NewGuid(),
                CategoryId = categoryId,
                BranchId = request.BranchId,
                IsAvailable = request.IsAvailable,
            });
        }
        else
        {
            availability.IsAvailable = request.IsAvailable;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
