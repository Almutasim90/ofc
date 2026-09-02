using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Payments;

public class BillSplitService(IAppDbContext db)
{
    public async Task<List<BillSplitDto>> ListAsync(Guid orderId, CancellationToken ct = default)
    {
        if (!await db.RestaurantOrders.AnyAsync(x => x.Id == orderId, ct)) throw new NotFoundException("Order not found.");
        var splits = await db.BillSplits.Where(x => x.OrderId == orderId)
            .Include(x => x.Lines).ThenInclude(x => x.OrderItem)
            .Include(x => x.Payments).OrderBy(x => x.CreatedAt).ToListAsync(ct);
        return splits.Select(ToDto).ToList();
    }

    public async Task<List<BillSplitDto>> CreateEqualAsync(Guid orderId, CreateEqualBillSplitsRequest request, CancellationToken ct = default)
    {
        if (request.ShareCount is < 2 or > 50) throw new ValidationException("Equal split count must be between 2 and 50.");
        var order = await LoadOrder(orderId, ct);
        EnsureCanSplit(order);
        if (order.Payments.Count > 0) throw new ValidationException("Create bill splits before recording payments.");
        var remaining = order.GrandTotal - order.BillSplits.Sum(x => x.Amount);
        if (remaining <= 0) throw new ValidationException("The order total is already fully allocated.");

        var baseAmount = Math.Floor(remaining * 1000 / request.ShareCount) / 1000;
        if (baseAmount <= 0) throw new ValidationException("The order total is too small for that many shares.");
        var createdAt = DateTime.UtcNow;
        var splits = Enumerable.Range(1, request.ShareCount).Select(index => new BillSplit
        {
            Id = Guid.NewGuid(), OrderId = order.Id, Name = $"Share {index}",
            Amount = index == request.ShareCount ? remaining - baseAmount * (request.ShareCount - 1) : baseAmount,
            CreatedAt = createdAt.AddTicks(index)
        }).ToList();
        db.BillSplits.AddRange(splits);
        order.PaymentRevision++;
        await db.SaveChangesAsync(ct);
        return splits.Select(ToDto).ToList();
    }

    public async Task<BillSplitDto> CreateItemAsync(Guid orderId, CreateItemBillSplitRequest request, CancellationToken ct = default)
    {
        if (request.Lines.Count == 0 || request.Lines.Any(x => x.Quantity <= 0)) throw new ValidationException("Select at least one positive item quantity.");
        if (request.Lines.Select(x => x.OrderItemId).Distinct().Count() != request.Lines.Count) throw new ValidationException("Duplicate order item allocation.");
        var order = await LoadOrder(orderId, ct);
        EnsureCanSplit(order);
        if (order.Payments.Count > 0) throw new ValidationException("Create bill splits before recording payments.");
        var activeItems = order.Items.Where(x => !x.IsCancelled).ToDictionary(x => x.Id);
        var allocatedQuantities = order.BillSplits.SelectMany(x => x.Lines).GroupBy(x => x.OrderItemId).ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));
        decimal selectedValue = 0;
        foreach (var line in request.Lines)
        {
            if (!activeItems.TryGetValue(line.OrderItemId, out var item)) throw new ValidationException("A selected item is not active on this order.");
            if (allocatedQuantities.GetValueOrDefault(item.Id) + line.Quantity > item.Quantity) throw new ValidationException($"Split quantity exceeds the active quantity for {item.MenuItemNameSnapshot}.");
            selectedValue += item.UnitPriceSnapshot * line.Quantity;
        }

        var activeValue = activeItems.Values.Sum(x => x.LineTotal);
        if (activeValue <= 0) throw new ValidationException("The order has no active item value to split.");
        var remaining = order.GrandTotal - order.BillSplits.Sum(x => x.Amount);
        var selectedQuantities = request.Lines.ToDictionary(x => x.OrderItemId, x => x.Quantity);
        var allocatesAllItems = activeItems.Values.All(x => allocatedQuantities.GetValueOrDefault(x.Id) + selectedQuantities.GetValueOrDefault(x.Id) == x.Quantity);
        var amount = allocatesAllItems ? remaining : Math.Round(order.GrandTotal * selectedValue / activeValue, 3, MidpointRounding.AwayFromZero);
        if (amount <= 0 || amount > remaining) throw new ValidationException("Split allocation exceeds the remaining order total.");

        var split = new BillSplit
        {
            Id = Guid.NewGuid(), OrderId = order.Id,
            Name = string.IsNullOrWhiteSpace(request.Name) ? $"Items {order.BillSplits.Count + 1}" : request.Name.Trim(),
            Amount = amount, CreatedAt = DateTime.UtcNow,
            Lines = request.Lines.Select(x => new BillSplitLine { Id = Guid.NewGuid(), OrderItemId = x.OrderItemId, Quantity = x.Quantity }).ToList()
        };
        if (split.Name.Length > 100) throw new ValidationException("Split name is too long.");
        db.BillSplits.Add(split);
        order.PaymentRevision++;
        await db.SaveChangesAsync(ct);
        foreach (var line in split.Lines) line.OrderItem = activeItems[line.OrderItemId];
        return ToDto(split);
    }

    private async Task<RestaurantOrder> LoadOrder(Guid orderId, CancellationToken ct) =>
        await db.RestaurantOrders.Include(x => x.Items).Include(x => x.Payments).Include(x => x.BillSplits).ThenInclude(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new NotFoundException("Order not found.");

    private static void EnsureCanSplit(RestaurantOrder order)
    {
        if (order.Status is not (RestaurantOrderStatuses.Open or RestaurantOrderStatuses.Sent))
            throw new ValidationException("This order cannot be split.");
        if (order.GrandTotal <= 0) throw new ValidationException("The order total must be positive.");
    }

    private static BillSplitDto ToDto(BillSplit split)
    {
        var paid = split.Payments.Sum(x => x.Amount);
        return new(split.Id, split.OrderId, split.Name, split.Amount, paid, split.Amount - paid, split.CreatedAt,
            split.Lines.Select(x => new BillSplitLineDto(x.OrderItemId, x.OrderItem.MenuItemNameSnapshot, x.Quantity)).ToList());
    }
}
