using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Ofc;

public class TableService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<List<TableDto>> GetAllAsync(Guid? branchId, CancellationToken cancellationToken = default)
    {
        var query = db.Tables.AsQueryable();
        if (branchId is not null) query = query.Where(t => t.BranchId == branchId);

        return await query
            .OrderBy(t => t.Label)
            .Select(t => new TableDto(t.Id, t.BranchId, t.Label, t.Capacity, t.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<TableDto> CreateAsync(CreateTableRequest request, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(request.BranchId);
        var labelTaken = await db.Tables.AnyAsync(t => t.BranchId == request.BranchId && t.Label == request.Label, cancellationToken);
        if (labelTaken)
            throw new ValidationException($"Table '{request.Label}' already exists for this branch.");

        var table = new Table
        {
            Id = Guid.NewGuid(),
            BranchId = request.BranchId,
            Label = request.Label,
            Capacity = request.Capacity,
            IsActive = true,
        };

        db.Tables.Add(table);
        await db.SaveChangesAsync(cancellationToken);

        return new TableDto(table.Id, table.BranchId, table.Label, table.Capacity, table.IsActive);
    }

    public async Task<TableDto> UpdateAsync(Guid id, UpdateTableRequest request, CancellationToken cancellationToken = default)
    {
        var table = await db.Tables.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new NotFoundException($"Table '{id}' not found.");
        EnsureBranchScope(table.BranchId);

        table.Label = request.Label;
        table.Capacity = request.Capacity;
        table.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return new TableDto(table.Id, table.BranchId, table.Label, table.Capacity, table.IsActive);
    }

    private void EnsureBranchScope(Guid branchId)
    {
        if (!currentUser.BypassBranchFilter && branchId != currentUser.BranchId)
            throw new ValidationException("You do not have access to manage this branch's data.");
    }
}
