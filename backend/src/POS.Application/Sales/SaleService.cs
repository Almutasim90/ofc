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

        ValidateDiscount(request.DiscountType, request.DiscountValue);
        foreach (var line in request.Lines) ValidateDiscount(line.DiscountType, line.DiscountValue);

        var userId = currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");
        var shift = await db.Shifts.FirstOrDefaultAsync(
            s => s.CashierUserId == userId && s.BranchId == request.BranchId && s.Status == ShiftStatus.Open,
            cancellationToken) ?? throw new ValidationException("An open shift is required before making a sale.");

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .ToListAsync(cancellationToken);
        if (products.Count != productIds.Count)
        {
            throw new ValidationException("One or more products are unavailable.");
        }

        var channelId = request.ChannelId ?? SalesChannelIds.InStore;
        var channel = await db.SalesChannels.FirstOrDefaultAsync(c => c.Id == channelId && c.IsActive, cancellationToken)
            ?? throw new ValidationException("The selected sales channel is unavailable.");
        var channelPrices = await db.ProductChannelPrices.Where(p => p.ChannelId == channelId && productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => p.Price, cancellationToken);

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
            ChannelId = channel.Id,
            ShiftId = shift.Id,
            CashierUserId = userId,
            BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow,
            PaymentMethod = request.PaymentMethod,
            DiscountType = NormalizeDiscountType(request.DiscountType),
            DiscountValue = request.DiscountValue,
            Status = SaleStatus.Completed,
        };

        decimal total = 0;
        decimal rawTotal = 0;
        foreach (var line in request.Lines)
        {
            var product = products.First(p => p.Id == line.ProductId);
            var unitPrice = channelPrices.GetValueOrDefault(product.Id, product.Price);
            var lineSubtotal = unitPrice * line.Quantity;
            rawTotal += lineSubtotal;
            var lineTotal = ApplyDiscount(lineSubtotal, line.DiscountType, line.DiscountValue);
            total += lineTotal;

            sale.Items.Add(new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = sale.Id,
                ProductId = product.Id,
                ProductNameSnapshot = product.NameAr,
                UnitPriceSnapshot = unitPrice,
                Quantity = line.Quantity,
                LineTotal = lineTotal,
                DiscountType = NormalizeDiscountType(line.DiscountType),
                DiscountValue = line.DiscountValue,
            });
        }
        sale.TotalAmount = ApplyDiscount(total, request.DiscountType, request.DiscountValue);
        sale.DiscountAmount = rawTotal - sale.TotalAmount;

        foreach (var (materialId, quantity) in requiredByMaterial)
        {
            sale.InventoryConsumptions.Add(new SaleInventoryConsumption
            {
                Id = Guid.NewGuid(), SaleId = sale.Id, RawMaterialId = materialId, QuantityConsumed = quantity,
            });
        }

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
            sale.Id, sale.BranchId, sale.ChannelId, sale.ShiftId, sale.CashierUserId, sale.BusinessDate, sale.CreatedAt, sale.TotalAmount,
            sale.DiscountType, sale.DiscountValue, sale.DiscountAmount,
            sale.PaymentMethod, sale.Status,
            sale.Items.Select(i => new SaleItemDto(i.ProductId, i.ProductNameSnapshot, i.UnitPriceSnapshot, i.Quantity,
                i.LineTotal, i.DiscountType, i.DiscountValue)).ToList());
    }

    private static string NormalizeDiscountType(string? type) => string.IsNullOrWhiteSpace(type) ? "None" : type;

    private static void ValidateDiscount(string? type, decimal value)
    {
        var normalized = NormalizeDiscountType(type);
        if (normalized is not ("None" or "Percentage" or "FixedAmount"))
            throw new ValidationException("Unknown discount type.");
        if (value < 0 || (normalized == "Percentage" && value > 100))
            throw new ValidationException("Discount value is invalid.");
    }

    private static decimal ApplyDiscount(decimal subtotal, string? type, decimal value)
    {
        var amount = NormalizeDiscountType(type) switch
        {
            "Percentage" => subtotal * value / 100m,
            "FixedAmount" => value,
            _ => 0m,
        };
        return Math.Max(0m, decimal.Round(subtotal - Math.Min(subtotal, amount), 3));
    }

    private void EnsureBranchScope(Guid branchId)
    {
        if (!currentUser.BypassBranchFilter && branchId != currentUser.BranchId)
        {
            throw new ValidationException("You do not have access to sell for this branch.");
        }
    }
}
