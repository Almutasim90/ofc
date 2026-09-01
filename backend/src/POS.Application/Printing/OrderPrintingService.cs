using System.Text;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Printing;

public class OrderPrintingService(IAppDbContext db, IRawPrinterClient printer)
{
    public async Task ConfirmAndPrintAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.RestaurantOrders.Include(x => x.OrderType).Include(x => x.Table).Include(x => x.Items).ThenInclude(x => x.MenuItem).ThenInclude(x => x.PrinterSection).ThenInclude(x => x!.PrinterConfig).FirstOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new NotFoundException("Order not found.");
        if (order.Status != RestaurantOrderStatuses.Open) throw new ValidationException("Only open orders can be confirmed.");
        var fallback = await db.PrinterConfigs.FirstOrDefaultAsync(x => x.BranchId == order.BranchId && x.IsDefault && x.IsActive, ct) ?? throw new ValidationException("An active default printer is required.");
        var jobs = BuildJobs(order, fallback);
        foreach (var job in jobs) await printer.SendAsync(job.IpAddress, job.Port, job.Payload, ct);
        order.Status = RestaurantOrderStatuses.Sent; await db.SaveChangesAsync(ct);
    }

    public static IReadOnlyList<PrintJob> BuildJobs(RestaurantOrder order, PrinterConfig fallback)
    {
        var activeItems = order.Items.Where(x => !x.IsCancelled).ToList(); var jobs = new List<PrintJob>();
        var location = order.OrderType.Code == "CAR_PICKUP" ? order.CarPlateNumber : order.Table?.Label;
        var locationLine = string.IsNullOrWhiteSpace(location) ? string.Empty : $"\nLOCATION {location}";
        foreach (var group in activeItems.GroupBy(x => x.MenuItem.PrinterSection))
        {
            var target = group.Key?.PrinterConfig is { IsActive: true } configured ? configured : fallback;
            var heading = group.Key?.NameEn ?? "Kitchen";
            jobs.Add(new(target.IpAddress, target.Port, EscPosDocument.Text($"{heading}\nORDER #{order.OrderNumber}{locationLine}\n" + string.Join('\n', group.Select(x => $"{x.Quantity} x {x.MenuItemNameSnapshot}"))), "Kitchen"));
        }
        jobs.Add(new(fallback.IpAddress, fallback.Port, EscPosDocument.Text($"OFC\nRECEIPT #{order.OrderNumber}{locationLine}\n" + string.Join('\n', activeItems.Select(x => $"{x.Quantity} x {x.MenuItemNameSnapshot}  {x.LineTotal:0.000}")) + $"\nTOTAL {order.GrandTotal:0.000}"), "Receipt"));
        return jobs;
    }
}

public static class EscPosDocument
{
    public static byte[] Text(string text)
    {
        var body = Encoding.UTF8.GetBytes(text.Replace("\r", string.Empty) + "\n\n\n");
        return [0x1B, 0x40, .. body, 0x1D, 0x56, 0x00];
    }
}
