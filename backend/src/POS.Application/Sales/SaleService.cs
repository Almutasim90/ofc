using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Domain.Events;

namespace POS.Application.Sales;

public class SaleService(IAppDbContext db, IDomainEventPublisher eventPublisher, ICurrentUserService currentUser)
{
    public async Task<SaleDto> CreateAsync(CreateSaleRequest request, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(request.BranchId);

        if (request.Lines.Count == 0)
        {
            throw new ValidationException("A sale must have at least one line item.");
        }

        if (request.Lines.Any(line => line.Quantity <= 0))
        {
            throw new ValidationException("Every sale line must have a quantity greater than zero.");
        }

        if (!PaymentMethods.All.Contains(request.PaymentMethod))
        {
            throw new ValidationException($"Unknown payment method '{request.PaymentMethod}'.");
        }

        var userId = currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToListAsync(cancellationToken);
        if (products.Count != productIds.Count)
        {
            throw new ValidationException("One or more products are unavailable.");
        }

        var branchIsActive = await db.Branches
            .AnyAsync(branch => branch.Id == request.BranchId && branch.IsActive, cancellationToken);
        if (!branchIsActive)
        {
            throw new ValidationException("The selected branch is unavailable.");
        }

        // A product with zero recipe rows for this branch sells with no stock deduction at
        // all (fresh/prepared-to-order items). Aggregate requirements across all lines first,
        // since two different products in the same sale may share a raw material.
        var recipes = await db.ProductRecipes
            .Where(r => productIds.Contains(r.ProductId) && r.BranchId == request.BranchId)
            .ToListAsync(cancellationToken);

        var requiredByMaterial = new Dictionary<Guid, decimal>();
        foreach (var line in request.Lines)
        {
            foreach (var recipeLine in recipes.Where(r => r.ProductId == line.ProductId))
            {
                requiredByMaterial[recipeLine.RawMaterialId] =
                    requiredByMaterial.GetValueOrDefault(recipeLine.RawMaterialId) + recipeLine.QuantityRequired * line.Quantity;
            }
        }

        var stockByMaterial = new Dictionary<Guid, BranchRawMaterialStock>();
        if (requiredByMaterial.Count > 0)
        {
            var materialIds = requiredByMaterial.Keys.ToList();
            var stocks = await db.BranchRawMaterialStocks
                .Where(s => s.BranchId == request.BranchId && materialIds.Contains(s.RawMaterialId))
                .ToListAsync(cancellationToken);
            stockByMaterial = stocks.ToDictionary(s => s.RawMaterialId);

            foreach (var (materialId, requiredQty) in requiredByMaterial)
            {
                var currentQty = stockByMaterial.TryGetValue(materialId, out var stock) ? stock.CurrentQuantity : 0m;
                if (currentQty < requiredQty)
                {
                    var material = await db.RawMaterials.FirstAsync(m => m.Id == materialId, cancellationToken);
                    throw new ValidationException(
                        $"Insufficient stock for '{material.NameEn}': need {requiredQty}, have {currentQty}.");
                }
            }
        }

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            BranchId = request.BranchId,
            ShiftId = null,
            CashierUserId = userId,
            BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow,
            PaymentMethod = request.PaymentMethod,
            Status = SaleStatus.Completed,
        };

        decimal total = 0;
        foreach (var line in request.Lines)
        {
            var product = products.First(p => p.Id == line.ProductId);
            var lineTotal = product.Price * line.Quantity;
            total += lineTotal;

            sale.Items.Add(new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = sale.Id,
                ProductId = product.Id,
                ProductNameSnapshot = product.NameAr,
                UnitPriceSnapshot = product.Price,
                Quantity = line.Quantity,
                LineTotal = lineTotal,
            });
        }
        sale.TotalAmount = total;

        db.Sales.Add(sale);

        foreach (var (materialId, requiredQty) in requiredByMaterial)
        {
            if (!stockByMaterial.TryGetValue(materialId, out var stock))
            {
                // Shouldn't happen - availability was checked above - but guard defensively.
                throw new ValidationException("Stock changed while processing the sale. Please retry.");
            }

            stock.CurrentQuantity -= requiredQty;
        }

        // One SaveChangesAsync call = one atomic transaction for the Sale, its SaleItems,
        // and every stock decrement together.
        await db.SaveChangesAsync(cancellationToken);

        await eventPublisher.PublishAsync(
            new SaleCompletedEvent(sale.Id, sale.BranchId, sale.CashierUserId, sale.CreatedAt), cancellationToken);

        return new SaleDto(
            sale.Id, sale.BranchId, sale.CashierUserId, sale.BusinessDate, sale.CreatedAt, sale.TotalAmount,
            sale.PaymentMethod, sale.Status,
            sale.Items.Select(i => new SaleItemDto(i.ProductId, i.ProductNameSnapshot, i.UnitPriceSnapshot, i.Quantity, i.LineTotal)).ToList());
    }

    private void EnsureBranchScope(Guid branchId)
    {
        if (!currentUser.BypassBranchFilter && branchId != currentUser.BranchId)
        {
            throw new ValidationException("You do not have access to sell for this branch.");
        }
    }
}
