using System.Text;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.RestaurantInventory;
using POS.Domain.Entities;
using POS.Application.Invoices;

namespace POS.Application.Printing;

public class OrderPrintingService(IAppDbContext db, IRawPrinterClient printer, RestaurantInventoryService inventory)
{
    public async Task ConfirmAndPrintAsync(Guid orderId, CancellationToken ct = default)
        => await ConfirmAndPrintAsync(orderId, null, false, ct);

    public async Task ConfirmQrAndPrintAsync(Guid orderId, Guid branchId, CancellationToken ct = default)
        => await ConfirmAndPrintAsync(orderId, branchId, true, ct);

    private async Task ConfirmAndPrintAsync(Guid orderId, Guid? capabilityBranchId, bool qrConfirmation, CancellationToken ct)
    {
        var orders = capabilityBranchId.HasValue ? db.RestaurantOrders.IgnoreQueryFilters() : db.RestaurantOrders;
        var order = await orders.Include(x => x.Branch).Include(x => x.OrderType).Include(x => x.Table).Include(x => x.Payments).ThenInclude(x => x.PaymentMethod).Include(x => x.Items).ThenInclude(x => x.MenuItem).ThenInclude(x => x.PrinterSection).ThenInclude(x => x!.PrinterConfig)
            .Include(x => x.Items).ThenInclude(x => x.Modifiers).ThenInclude(x => x.ModifierOption)
            .Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.ComboComponent)
            .Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.SelectedMenuItem)
            .FirstOrDefaultAsync(x => x.Id == orderId && (!capabilityBranchId.HasValue || x.BranchId == capabilityBranchId), ct) ?? throw new NotFoundException("Order not found.");
        if (order.Status != RestaurantOrderStatuses.Open && (!qrConfirmation || order.Status is not (RestaurantOrderStatuses.PendingApproval or RestaurantOrderStatuses.Sent or RestaurantOrderStatuses.Paid))) throw new ValidationException("Only open or approved QR orders can be confirmed.");
        var printers = capabilityBranchId.HasValue ? db.PrinterConfigs.IgnoreQueryFilters() : db.PrinterConfigs;
        var fallback = await printers.FirstOrDefaultAsync(x => x.BranchId == order.BranchId && x.IsDefault && x.IsActive, ct) ?? throw new ValidationException("An active default printer is required.");
        var jobs = BuildJobs(order, fallback);
        await inventory.Confirm(orderId, capabilityBranchId, qrConfirmation, ct);
        foreach (var job in jobs) await printer.SendAsync(job.IpAddress, job.Port, job.Payload, ct);
    }

    public async Task PrintCustomerInvoiceAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.RestaurantOrders.Include(x => x.Branch).Include(x => x.OrderType).Include(x => x.Table).Include(x => x.Payments).ThenInclude(x => x.PaymentMethod)
            .Include(x => x.Items).ThenInclude(x => x.Modifiers).ThenInclude(x => x.ModifierOption)
            .Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.ComboComponent)
            .Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.SelectedMenuItem)
            .FirstOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new NotFoundException("Order not found.");
        var printerConfig = await db.PrinterConfigs.FirstOrDefaultAsync(x => x.BranchId == order.BranchId && x.IsDefault && x.IsActive, ct) ?? throw new ValidationException("An active default printer is required.");
        await printer.SendAsync(printerConfig.IpAddress, printerConfig.Port, BuildCustomerReceipt(InvoiceService.BuildDocument(order)), ct);
    }

    public static IReadOnlyList<PrintJob> BuildJobs(RestaurantOrder order, PrinterConfig fallback)
    {
        var activeItems = order.Items.Where(x => !x.IsCancelled).ToList(); var jobs = new List<PrintJob>();
        var location = order.OrderType.Code == "CAR_PICKUP" ? order.CarPlateNumber : order.Table?.Label;
        var locationLine = string.IsNullOrWhiteSpace(location) ? string.Empty : $"\nLOCATION {location}";
        foreach (var group in activeItems.GroupBy(x => x.MenuItem.PrinterSection))
        {
            var target = group.Key is { BranchId: var branchId, PrinterConfig.IsActive: true } && branchId == order.BranchId
                ? group.Key.PrinterConfig
                : fallback;
            var heading = group.Key?.BranchId == order.BranchId ? group.Key.NameEn : "Kitchen";
            jobs.Add(new(target.IpAddress, target.Port, EscPosDocument.Text($"{heading}\nORDER #{order.OrderNumber}{locationLine}\n" + string.Join('\n', group.Select(x => FormatLine(x, false)))), "Kitchen"));
        }
        jobs.Add(new(fallback.IpAddress, fallback.Port, BuildCustomerReceipt(InvoiceService.BuildDocument(order)), "Receipt"));
        return jobs;
    }

    public static byte[] BuildCustomerReceipt(InvoiceDocument invoice)
    {
        var lines = new List<string> { invoice.LegalNameEn, invoice.LegalNameAr };
        if (!string.IsNullOrWhiteSpace(invoice.TaxRegistrationNumber)) lines.Add($"TAX REG {invoice.TaxRegistrationNumber}");
        if (!string.IsNullOrWhiteSpace(invoice.CommercialRegistrationNumber)) lines.Add($"CR {invoice.CommercialRegistrationNumber}");
        if (!string.IsNullOrWhiteSpace(invoice.AddressEn)) lines.Add(invoice.AddressEn); if (!string.IsNullOrWhiteSpace(invoice.AddressAr)) lines.Add(invoice.AddressAr);
        if (!string.IsNullOrWhiteSpace(invoice.Phone)) lines.Add($"TEL {invoice.Phone}");
        lines.Add($"TAX INVOICE / ORDER #{invoice.InvoiceNumber}"); lines.Add($"DATE {invoice.Date:yyyy-MM-dd HH:mm:ss}"); if (!string.IsNullOrWhiteSpace(invoice.Location)) lines.Add($"LOCATION {invoice.Location}");
        lines.Add("--------------------------------"); lines.Add("ITEM/QTY       NET    TAX  GROSS");
        foreach (var line in invoice.Lines) { lines.Add($"{line.Quantity} x {line.Name}"); lines.Add($"  {line.Net:0.000} {line.Tax:0.000} {line.Gross:0.000}"); lines.AddRange(line.Details.Select(x => $"  {x}")); if (!string.IsNullOrWhiteSpace(line.Notes)) lines.Add($"  NOTE: {line.Notes}"); }
        lines.Add("--------------------------------"); lines.Add($"SUBTOTAL {invoice.Subtotal:0.000} {invoice.Currency}"); lines.Add($"DISCOUNT {invoice.Discount:0.000} {invoice.Currency}"); lines.Add($"TAX {invoice.Tax:0.000} {invoice.Currency}"); lines.Add($"GRAND TOTAL {invoice.GrandTotal:0.000} {invoice.Currency}");
        if (invoice.Payments.Count > 0) { lines.Add("PAYMENTS"); lines.AddRange(invoice.Payments.Select(x => $"{x.NameEn}/{x.NameAr} {x.Amount:0.000}")); }
        if (!string.IsNullOrWhiteSpace(invoice.Footer)) lines.Add(invoice.Footer);
        return EscPosDocument.Text(string.Join('\n', lines));
    }

    private static string FormatLine(RestaurantOrderItem item, bool includeTotal)
    {
        var details = item.Modifiers.Select(x => $"  + {x.ModifierOption.NameEn} ({x.PriceDeltaSnapshot:+0.000;-0.000;0.000})")
            .Concat(item.ComboSelections.Select(x => $"  {x.ComboComponent.SlotLabel}: {x.SelectedMenuItem.NameEn} ({x.PriceDeltaSnapshot:+0.000;-0.000;0.000})"));
        if (!string.IsNullOrWhiteSpace(item.Notes)) details = details.Append($"  NOTE: {item.Notes}");
        var heading = $"{item.Quantity} x {item.MenuItemNameSnapshot}" + (includeTotal ? $"  {item.LineTotal:0.000}" : string.Empty);
        return string.Join('\n', details.Prepend(heading));
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
