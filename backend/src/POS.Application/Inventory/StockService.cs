using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Inventory;

public class StockService(IAppDbContext db, ICurrentUserService currentUser)
{
    public async Task<List<StockStatusDto>> GetStatusAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(branchId);

        var materials = await db.RawMaterials.ToListAsync(cancellationToken);
        var stocks = await db.BranchRawMaterialStocks
            .Where(s => s.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var stockByMaterial = stocks.ToDictionary(s => s.RawMaterialId);

        return materials
            .Select(m =>
            {
                var hasStock = stockByMaterial.TryGetValue(m.Id, out var stock);
                var currentQuantity = hasStock ? stock!.CurrentQuantity : 0m;
                var threshold = hasStock ? stock!.LowStockThreshold : 0m;
                return new StockStatusDto(m.Id, m.NameAr, m.NameEn, m.Unit, currentQuantity, threshold, currentQuantity <= threshold);
            })
            .OrderBy(s => s.NameEn)
            .ToList();
    }

    public async Task AdjustAsync(AdjustStockRequest request, Guid adjustedByUserId, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(request.BranchId);

        var materialExists = await db.RawMaterials.AnyAsync(m => m.Id == request.RawMaterialId, cancellationToken);
        if (!materialExists)
        {
            throw new NotFoundException($"Raw material '{request.RawMaterialId}' not found.");
        }

        var stock = await db.BranchRawMaterialStocks
            .FirstOrDefaultAsync(s => s.BranchId == request.BranchId && s.RawMaterialId == request.RawMaterialId, cancellationToken);

        if (stock is null)
        {
            stock = new BranchRawMaterialStock
            {
                BranchId = request.BranchId,
                RawMaterialId = request.RawMaterialId,
                CurrentQuantity = 0,
                LowStockThreshold = 0,
            };
            db.BranchRawMaterialStocks.Add(stock);
        }

        stock.CurrentQuantity += request.QuantityChange;

        db.StockAdjustments.Add(new StockAdjustment
        {
            Id = Guid.NewGuid(),
            BranchId = request.BranchId,
            RawMaterialId = request.RawMaterialId,
            QuantityChange = request.QuantityChange,
            Reason = request.Reason,
            AdjustedByUserId = adjustedByUserId,
            AdjustedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetLowStockThresholdAsync(SetLowStockThresholdRequest request, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(request.BranchId);

        var stock = await db.BranchRawMaterialStocks
            .FirstOrDefaultAsync(s => s.BranchId == request.BranchId && s.RawMaterialId == request.RawMaterialId, cancellationToken);

        if (stock is null)
        {
            var materialExists = await db.RawMaterials.AnyAsync(m => m.Id == request.RawMaterialId, cancellationToken);
            if (!materialExists)
            {
                throw new NotFoundException($"Raw material '{request.RawMaterialId}' not found.");
            }

            stock = new BranchRawMaterialStock
            {
                BranchId = request.BranchId,
                RawMaterialId = request.RawMaterialId,
                CurrentQuantity = 0,
                LowStockThreshold = request.Threshold,
            };
            db.BranchRawMaterialStocks.Add(stock);
        }
        else
        {
            stock.LowStockThreshold = request.Threshold;
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
