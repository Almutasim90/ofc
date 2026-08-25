using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Closing;
using POS.Domain.Entities;

namespace POS.Application.Inventory;

public class StockService(IAppDbContext db, ICurrentUserService currentUser)
{
    private static readonly IReadOnlyDictionary<string, string> BaseUnits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    { ["Weight"] = "kg", ["Volume"] = "ml", ["Count"] = "piece" };
    public async Task<List<StockStatusDto>> GetStatusAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(branchId);

        var materials = await db.RawMaterials.ToListAsync(cancellationToken);
        var stocks = await db.BranchRawMaterialStocks
            .Where(s => s.BranchId == branchId)
            .ToListAsync(cancellationToken);
        var stockByMaterial = stocks.ToDictionary(s => s.RawMaterialId);
        var packages = await db.SupplyPackages.AsNoTracking().Where(x => x.IsActive).ToListAsync(cancellationToken);
        var packageByMaterial = packages.GroupBy(x => x.RawMaterialId).ToDictionary(x => x.Key, x => x.First());

        return materials
            .Select(m =>
            {
                var hasStock = stockByMaterial.TryGetValue(m.Id, out var stock);
                var currentQuantity = hasStock ? stock!.CurrentQuantity : 0m;
                var threshold = hasStock ? stock!.LowStockThreshold : 0m;
                packageByMaterial.TryGetValue(m.Id, out var package);
                return new StockStatusDto(m.Id, m.NameAr, m.NameEn, m.Unit, currentQuantity, threshold,
                    currentQuantity <= threshold, package?.Id, package?.NameAr, package?.NameEn, package?.BaseQuantity);
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

    public async Task<List<SupplyPackageDto>> GetSupplyPackagesAsync(Guid? rawMaterialId, CancellationToken ct = default) =>
        await db.SupplyPackages.AsNoTracking()
            .Where(x => (!rawMaterialId.HasValue || x.RawMaterialId == rawMaterialId) && x.IsActive)
            .OrderBy(x => x.NameEn)
            .Select(x => new SupplyPackageDto(x.Id, x.RawMaterialId, x.NameAr, x.NameEn, x.BaseQuantity, x.IsActive))
            .ToListAsync(ct);

    public async Task<SupplyPackageDto> CreateSupplyPackageAsync(UpsertSupplyPackageRequest request, CancellationToken ct = default)
    {
        if (request.BaseQuantity <= 0) throw new ValidationException("Package conversion quantity must be greater than zero.");
        if (string.IsNullOrWhiteSpace(request.NameAr) || string.IsNullOrWhiteSpace(request.NameEn))
            throw new ValidationException("Package names are required.");
        if (!await db.RawMaterials.AnyAsync(x => x.Id == request.RawMaterialId, ct)) throw new NotFoundException("Raw material not found.");
        if (await db.SupplyPackages.AnyAsync(x => x.RawMaterialId == request.RawMaterialId, ct))
            throw new ValidationException("The purchase package and conversion are permanent and were already configured for this raw material.");
        var item = new SupplyPackage { Id = Guid.NewGuid(), RawMaterialId = request.RawMaterialId,
            NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(), BaseQuantity = request.BaseQuantity, IsActive = request.IsActive };
        db.SupplyPackages.Add(item); await db.SaveChangesAsync(ct);
        return new(item.Id, item.RawMaterialId, item.NameAr, item.NameEn, item.BaseQuantity, item.IsActive);
    }

    public async Task<StockReceiptDto> ReceiveAsync(ReceiveStockRequest request, Guid userId, CancellationToken ct = default)
    {
        EnsureBranchScope(request.BranchId);
        if (request.PackageCount <= 0) throw new ValidationException("Package count must be greater than zero.");
        var package = await db.SupplyPackages.Include(x => x.RawMaterial)
            .FirstOrDefaultAsync(x => x.Id == request.SupplyPackageId && x.IsActive, ct) ?? throw new NotFoundException("Supply package not found.");
        var quantityAdded = InventoryQuantityCalculator.FromPackages(package.BaseQuantity, request.PackageCount);
        var stock = await db.BranchRawMaterialStocks.FirstOrDefaultAsync(x => x.BranchId == request.BranchId && x.RawMaterialId == package.RawMaterialId, ct);
        if (stock is null)
        {
            stock = new BranchRawMaterialStock { BranchId = request.BranchId, RawMaterialId = package.RawMaterialId };
            db.BranchRawMaterialStocks.Add(stock);
        }
        stock.CurrentQuantity += quantityAdded;
        var receivedAt = request.ReceivedDate.HasValue
            ? MuscatClock.ToUtc(request.ReceivedDate.Value.ToDateTime(new TimeOnly(12, 0)))
            : DateTime.UtcNow;
        var receipt = new StockReceipt { Id = Guid.NewGuid(), BranchId = request.BranchId, RawMaterialId = package.RawMaterialId,
            SupplyPackageId = package.Id, PackageCount = request.PackageCount, BaseQuantityAdded = quantityAdded,
            PackageNameSnapshot = package.NameAr, Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            ReceivedByUserId = userId, ReceivedAt = receivedAt };
        db.StockReceipts.Add(receipt);
        db.StockAdjustments.Add(new StockAdjustment { Id = Guid.NewGuid(), BranchId = request.BranchId,
            RawMaterialId = package.RawMaterialId, QuantityChange = quantityAdded, Reason = $"Receipt: {request.PackageCount} × {package.NameAr}",
            AdjustedByUserId = userId, AdjustedAt = receipt.ReceivedAt });
        await db.SaveChangesAsync(ct);
        return new(receipt.Id, receipt.BranchId, receipt.RawMaterialId, package.RawMaterial.NameAr, package.RawMaterial.NameEn,
            package.RawMaterial.Unit, package.Id, package.NameAr, receipt.PackageCount, receipt.BaseQuantityAdded, receipt.Note, receipt.ReceivedAt);
    }

    public async Task<List<StockReceiptDto>> GetRecentReceiptsAsync(Guid branchId, CancellationToken ct = default)
    {
        EnsureBranchScope(branchId);
        return await db.StockReceipts.AsNoTracking().Where(x => x.BranchId == branchId).OrderByDescending(x => x.ReceivedAt).Take(50)
            .Select(x => new StockReceiptDto(x.Id, x.BranchId, x.RawMaterialId, x.RawMaterial.NameAr, x.RawMaterial.NameEn,
                x.RawMaterial.Unit, x.SupplyPackageId, x.PackageNameSnapshot, x.PackageCount, x.BaseQuantityAdded, x.Note, x.ReceivedAt)).ToListAsync(ct);
    }

    public async Task<CreateInventoryItemResult> CreateInventoryItemAsync(CreateInventoryItemRequest request, Guid userId, CancellationToken ct = default)
    {
        EnsureBranchScope(request.BranchId);
        if (!BaseUnits.TryGetValue(request.MeasurementType, out var unit))
            throw new ValidationException("Measurement type must be Weight, Volume, or Count.");
        if (string.IsNullOrWhiteSpace(request.NameAr) || string.IsNullOrWhiteSpace(request.NameEn)
            || string.IsNullOrWhiteSpace(request.PackageNameAr) || string.IsNullOrWhiteSpace(request.PackageNameEn))
            throw new ValidationException("Material and package names are required.");
        if (request.BaseQuantityPerPackage <= 0 || request.InitialPackageCount < 0 || request.LowStockThreshold < 0)
            throw new ValidationException("Package quantity must be positive and stock values cannot be negative.");
        if (!await db.Branches.AnyAsync(x => x.Id == request.BranchId && x.IsActive, ct))
            throw new ValidationException("The selected branch is unavailable.");
        if (await db.RawMaterials.AnyAsync(x => x.NameAr == request.NameAr.Trim() || x.NameEn == request.NameEn.Trim(), ct))
            throw new ValidationException("A raw material with this name already exists.");

        var material = new RawMaterial { Id = Guid.NewGuid(), NameAr = request.NameAr.Trim(), NameEn = request.NameEn.Trim(),
            MeasurementType = request.MeasurementType, Unit = unit };
        var package = new SupplyPackage { Id = Guid.NewGuid(), RawMaterialId = material.Id, NameAr = request.PackageNameAr.Trim(),
            NameEn = request.PackageNameEn.Trim(), BaseQuantity = request.BaseQuantityPerPackage, IsActive = true };
        var initialQuantity = InventoryQuantityCalculator.FromPackages(request.BaseQuantityPerPackage, request.InitialPackageCount);
        var stock = new BranchRawMaterialStock { BranchId = request.BranchId, RawMaterialId = material.Id,
            CurrentQuantity = initialQuantity, LowStockThreshold = request.LowStockThreshold };
        db.RawMaterials.Add(material); db.SupplyPackages.Add(package); db.BranchRawMaterialStocks.Add(stock);
        if (initialQuantity > 0)
        {
            var now = DateTime.UtcNow;
            db.StockReceipts.Add(new StockReceipt { Id = Guid.NewGuid(), BranchId = request.BranchId, RawMaterialId = material.Id,
                SupplyPackageId = package.Id, PackageCount = request.InitialPackageCount, BaseQuantityAdded = initialQuantity,
                PackageNameSnapshot = package.NameAr, Note = request.Note, ReceivedByUserId = userId, ReceivedAt = now });
            db.StockAdjustments.Add(new StockAdjustment { Id = Guid.NewGuid(), BranchId = request.BranchId, RawMaterialId = material.Id,
                QuantityChange = initialQuantity, Reason = $"Initial receipt: {request.InitialPackageCount} × {package.NameAr}",
                AdjustedByUserId = userId, AdjustedAt = now });
        }
        await db.SaveChangesAsync(ct);
        return new(material.Id, package.Id, initialQuantity);
    }

    private void EnsureBranchScope(Guid branchId)
    {
        if (!currentUser.BypassBranchFilter && branchId != currentUser.BranchId)
        {
            throw new ValidationException("You do not have access to manage this branch's data.");
        }
    }
}
