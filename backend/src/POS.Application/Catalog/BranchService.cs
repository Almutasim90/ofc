using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Catalog;

public class BranchService(IAppDbContext db)
{
    public async Task<List<BranchDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Branches
            .OrderBy(b => b.NameEn)
            .Select(b => new BranchDto(b.Id, b.NameAr, b.NameEn, b.Code, b.DefaultOpeningFloat, b.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var codeTaken = await db.Branches.AnyAsync(b => b.Code == request.Code, cancellationToken);
        if (codeTaken)
        {
            throw new ValidationException($"Branch code '{request.Code}' is already taken.");
        }
        if (request.DefaultOpeningFloat < 0)
            throw new ValidationException("Default opening float cannot be negative.");

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            NameAr = request.NameAr,
            NameEn = request.NameEn,
            Code = request.Code,
            DefaultOpeningFloat = request.DefaultOpeningFloat,
            IsActive = true,
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync(cancellationToken);

        return new BranchDto(branch.Id, branch.NameAr, branch.NameEn, branch.Code, branch.DefaultOpeningFloat, branch.IsActive);
    }

    public async Task<BranchDto> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Branch '{id}' not found.");
        if (request.DefaultOpeningFloat < 0)
            throw new ValidationException("Default opening float cannot be negative.");

        branch.NameAr = request.NameAr;
        branch.NameEn = request.NameEn;
        branch.Code = request.Code;
        branch.DefaultOpeningFloat = request.DefaultOpeningFloat;
        branch.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return new BranchDto(branch.Id, branch.NameAr, branch.NameEn, branch.Code, branch.DefaultOpeningFloat, branch.IsActive);
    }
}
