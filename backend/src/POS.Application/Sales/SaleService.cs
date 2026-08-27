using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Domain.Events;
using POS.Application.Closing;

namespace POS.Application.Sales;

public class SaleService(IAppDbContext db, IDomainEventPublisher eventPublisher, ICurrentUserService currentUser)
{
    public Task<SaleDto> CreateAsync(CreateSaleRequest request, CancellationToken cancellationToken = default)
        => SaveAsync(request, null, null, cancellationToken);

    private bool CanManage(Sale sale) => currentUser.RoleName != RoleNames.Cashier || sale.CashierUserId == currentUser.UserId;

    public async Task<List<SaleDto>> ListAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(branchId);
        var query = db.Sales.AsNoTracking().Include(s => s.Items).Include(s => s.Shift).Where(s => s.BranchId == branchId);
        if (currentUser.RoleName == RoleNames.Cashier) query = query.Where(s => s.CashierUserId == currentUser.UserId);
        var sales = await query.OrderByDescending(s => s.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return sales.Select(ToDto).ToList();
    }

    public async Task<List<SaleEditDto>> HistoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sale = await db.Sales.FirstOrDefaultAsync(s => s.Id == id, cancellationToken) ?? throw new NotFoundException("Sale not found.");
        EnsureBranchScope(sale.BranchId);
        if (!CanManage(sale)) throw new ForbiddenException("You can only access your own sales.");
        var history = await db.SaleEdits.AsNoTracking().Where(e => e.SaleId == id).OrderByDescending(e => e.CreatedAt).ToListAsync(cancellationToken);
        return history.Select(e => new SaleEditDto(e.Id, e.EditedByUserId, e.EditedByName, e.CreatedAt, e.Reason,
            JsonSerializer.Deserialize<SaleDto>(e.BeforeJson)!, JsonSerializer.Deserialize<SaleDto>(e.AfterJson)!)).ToList();
    }

    public async Task<SaleDto> UpdateAsync(Guid id, UpdateSaleRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.Permissions.Contains(PermissionKeys.SalesEdit)) throw new ForbiddenException("Sale edit permission is required.");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            throw new ValidationException("A reason of up to 1000 characters is required.");
        var sale = await db.Sales.Include(s => s.Items).Include(s => s.InventoryConsumptions).Include(s => s.Shift)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken) ?? throw new NotFoundException("Sale not found.");
        EnsureBranchScope(sale.BranchId);
        if (!CanManage(sale)) throw new ForbiddenException("You can only edit your own sales.");
        if (sale.Status != SaleStatus.Completed || sale.Shift.Status != ShiftStatus.Open)
            throw new ValidationException("Only completed sales in an open shift can be edited.");
        if (sale.Revision != request.Revision) throw new DbUpdateConcurrencyException();
        if (request.Sale.BranchId != sale.BranchId || (request.Sale.ChannelId ?? SalesChannelIds.InStore) != sale.ChannelId)
            throw new ValidationException("The branch and channel of a saved sale cannot be changed.");
        return await SaveAsync(request.Sale, sale, request.Reason.Trim(), cancellationToken);
    }

    private async Task<SaleDto> SaveAsync(CreateSaleRequest request, Sale? existing, string? reason, CancellationToken cancellationToken)
    {
        EnsureBranchScope(request.BranchId);

        if (request.Lines is null || request.Lines.Count == 0 || request.Lines.Count > 200)
        {
            throw new ValidationException("A sale must have at least one line item.");
        }

        if (request.Lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1))
            throw new ValidationException("Duplicate product lines are not allowed.");

        if (request.Lines.Any(line => line.Quantity <= 0 || decimal.Round(line.Quantity, 3) != line.Quantity))
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
        var shift = existing?.Shift ?? await db.Shifts.FirstOrDefaultAsync(
            s => s.CashierUserId == userId && s.BranchId == request.BranchId && s.Status == ShiftStatus.Open,
            cancellationToken) ?? throw new ValidationException("An open shift is required before making a sale.");

        var before = existing is null ? null : ToDto(existing);
        var oldItems = existing?.Items.ToDictionary(i => i.ProductId) ?? new Dictionary<Guid, SaleItem>();
        var oldConsumption = existing?.InventoryConsumptions.ToDictionary(c => c.RawMaterialId, c => c.QuantityConsumed) ?? new Dictionary<Guid, decimal>();
        var quantitiesChanged = existing is null || request.Lines.Count != oldItems.Count || request.Lines.Any(l => !oldItems.TryGetValue(l.ProductId, out var old) || old.Quantity != l.Quantity);

        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
        if (products.Count != productIds.Count || products.Any(p => !p.IsActive && !oldItems.ContainsKey(p.Id)))
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

        var recipeSnapshots = new Dictionary<Guid, Dictionary<Guid, decimal>>();
        var requiredByMaterial = new Dictionary<Guid, decimal>();
        foreach (var line in request.Lines)
        {
            var snapshot = oldItems.TryGetValue(line.ProductId, out var old) && old.RecipeSnapshotJson is not null
                ? JsonSerializer.Deserialize<Dictionary<Guid, decimal>>(old.RecipeSnapshotJson)!
                : recipes.Where(r => r.ProductId == line.ProductId).ToDictionary(r => r.RawMaterialId, r => r.QuantityRequired);
            recipeSnapshots[line.ProductId] = snapshot;
            foreach (var (material, quantity) in snapshot)
                requiredByMaterial[material] = requiredByMaterial.GetValueOrDefault(material) + quantity * line.Quantity;
        }

        var stockByMaterial = new Dictionary<Guid, BranchRawMaterialStock>();
        if (quantitiesChanged && (requiredByMaterial.Count > 0 || oldConsumption.Count > 0))
        {
            var materialIds = requiredByMaterial.Keys.Union(oldConsumption.Keys).ToList();
            var stocks = await db.BranchRawMaterialStocks
                .Where(s => s.BranchId == request.BranchId && materialIds.Contains(s.RawMaterialId))
                .ToListAsync(cancellationToken);
            stockByMaterial = stocks.ToDictionary(s => s.RawMaterialId);
        }

        var sale = existing ?? new Sale
        {
            Id = Guid.NewGuid(),
            BranchId = request.BranchId,
            ChannelId = channel.Id,
            ShiftId = shift.Id,
            Shift = shift,
            CashierUserId = userId,
            BusinessDate = DateOnly.FromDateTime(MuscatClock.ToLocal(DateTime.UtcNow)),
            CreatedAt = DateTime.UtcNow,
            PaymentMethod = request.PaymentMethod,
            DiscountType = NormalizeDiscountType(request.DiscountType),
            DiscountValue = request.DiscountValue,
            Status = SaleStatus.Completed,
        };

        sale.PaymentMethod = request.PaymentMethod;
        sale.DiscountType = NormalizeDiscountType(request.DiscountType);
        sale.DiscountValue = request.DiscountValue;
        if (existing is not null)
        {
            db.SaleItems.RemoveRange(sale.Items);
            sale.Items.Clear();
        }
        decimal total = 0;
        decimal rawTotal = 0;
        foreach (var line in request.Lines)
        {
            var product = products.First(p => p.Id == line.ProductId);
            var unitPrice = oldItems.TryGetValue(product.Id, out var prior) ? prior.UnitPriceSnapshot : channelPrices.GetValueOrDefault(product.Id, product.Price);
            var lineSubtotal = unitPrice * line.Quantity;
            rawTotal += lineSubtotal;
            var lineTotal = ApplyDiscount(lineSubtotal, line.DiscountType, line.DiscountValue);
            total += lineTotal;

            sale.Items.Add(new SaleItem
            {
                Id = Guid.NewGuid(),
                SaleId = sale.Id,
                ProductId = product.Id,
                ProductNameSnapshot = prior?.ProductNameSnapshot ?? product.NameAr,
                RecipeSnapshotJson = JsonSerializer.Serialize(recipeSnapshots[product.Id]),
                UnitPriceSnapshot = unitPrice,
                Quantity = line.Quantity,
                LineTotal = lineTotal,
                DiscountType = NormalizeDiscountType(line.DiscountType),
                DiscountValue = line.DiscountValue,
            });
        }
        sale.TotalAmount = ApplyDiscount(total, request.DiscountType, request.DiscountValue);
        sale.DiscountAmount = rawTotal - sale.TotalAmount;

        (sale.CashAmount, sale.CardAmount) = SalePaymentCalculator.Calculate(request.PaymentMethod, sale.TotalAmount, request.CashAmount, request.CardAmount);
        if (quantitiesChanged)
        {
            db.SaleInventoryConsumptions.RemoveRange(sale.InventoryConsumptions);
            sale.InventoryConsumptions.Clear();
            foreach (var materialId in requiredByMaterial.Keys.Union(oldConsumption.Keys))
            {
                if (!stockByMaterial.TryGetValue(materialId, out var stock))
                {
                    stock = new BranchRawMaterialStock { BranchId = request.BranchId, RawMaterialId = materialId };
                    db.BranchRawMaterialStocks.Add(stock);
                }
                var available = stock.CurrentQuantity + oldConsumption.GetValueOrDefault(materialId);
                var consumed = Math.Min(Math.Max(0, available), requiredByMaterial.GetValueOrDefault(materialId));
                stock.CurrentQuantity = Math.Max(0, available - consumed);
                sale.InventoryConsumptions.Add(new SaleInventoryConsumption {
                    Id = Guid.NewGuid(), SaleId = sale.Id, RawMaterialId = materialId, QuantityConsumed = consumed,
                });
            }
        }
        if (existing is not null)
        {
            db.SaleItems.AddRange(sale.Items);
            if (quantitiesChanged) db.SaleInventoryConsumptions.AddRange(sale.InventoryConsumptions);
        }
        shift.SalesRevision++;
        if (existing is null) db.Sales.Add(sale);
        else
        {
            sale.Revision++;
            var editorName = await db.Users.Where(u => u.Id == userId).Select(u => u.FullName).FirstAsync(cancellationToken);
            db.SaleEdits.Add(new SaleEdit {
                Id = Guid.NewGuid(), SaleId = sale.Id, EditedByUserId = userId, EditedByName = editorName,
                CreatedAt = DateTime.UtcNow, Reason = reason!, BeforeJson = JsonSerializer.Serialize(before), AfterJson = JsonSerializer.Serialize(ToDto(sale)),
            });
        }

        // One SaveChangesAsync call = one atomic transaction for the Sale, its SaleItems,
        // and every stock decrement together.
        await db.SaveChangesAsync(cancellationToken);

        if (existing is null)
            await eventPublisher.PublishAsync(
                new SaleCompletedEvent(sale.Id, sale.BranchId, sale.CashierUserId, sale.CreatedAt), cancellationToken);

        return ToDto(sale);
    }

    private SaleDto ToDto(Sale sale) => new(
        sale.Id, sale.BranchId, sale.ChannelId, sale.ShiftId, sale.CashierUserId, sale.BusinessDate, sale.CreatedAt, sale.TotalAmount,
        sale.DiscountType, sale.DiscountValue, sale.DiscountAmount, sale.PaymentMethod, sale.Status,
        sale.Items.Select(i => new SaleItemDto(i.ProductId, i.ProductNameSnapshot, i.UnitPriceSnapshot, i.Quantity,
            i.LineTotal, i.DiscountType, i.DiscountValue)).ToList(),
        sale.CashAmount ?? (sale.PaymentMethod == PaymentMethods.Cash ? sale.TotalAmount : 0),
        sale.CardAmount ?? (sale.PaymentMethod == PaymentMethods.Card ? sale.TotalAmount : 0), sale.Revision,
        sale.Status == SaleStatus.Completed && sale.Shift.Status == ShiftStatus.Open && CanManage(sale) && currentUser.Permissions.Contains(PermissionKeys.SalesEdit));

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
        // Global reporting access must not grant cross-branch sales editing.
        if (currentUser.RoleName != RoleNames.GeneralManager && branchId != currentUser.BranchId)
        {
            throw new ValidationException("You do not have access to sell for this branch.");
        }
    }
}
