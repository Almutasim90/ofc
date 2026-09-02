using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace POS.Application.Invoices;

public record InvoiceLineDocument(Guid Id, string Name, int Quantity, decimal UnitPrice, decimal TaxRate, decimal Net, decimal Tax, decimal Gross, string? Notes, IReadOnlyList<string> Details);
public record InvoicePaymentDocument(string Code, string NameAr, string NameEn, decimal Amount);
public record InvoiceDocument(Guid OrderId, int InvoiceNumber, DateTime Date, string? Location, string Currency, bool PricesIncludeTax, string LegalNameAr, string LegalNameEn, string? TaxRegistrationNumber, string? CommercialRegistrationNumber, string? AddressAr, string? AddressEn, string? Phone, string? Footer, decimal Subtotal, decimal Discount, decimal Tax, decimal GrandTotal, IReadOnlyList<InvoiceLineDocument> Lines, IReadOnlyList<InvoicePaymentDocument> Payments);

public class InvoiceService(IAppDbContext db)
{
    static InvoiceService() => QuestPDF.Settings.License = LicenseType.Community;
    public static decimal Round(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    public static void ApplySettings(RestaurantOrder order, InvoiceSettingsDto settings)
    {
        if (order.InvoicePricesIncludeTax is not null) return;
        order.InvoicePricesIncludeTax = settings.PricesIncludeTax; order.InvoiceDefaultTaxRate = settings.DefaultTaxRate; order.InvoiceCurrency = settings.Currency;
        order.InvoiceLegalNameAr = settings.LegalNameAr; order.InvoiceLegalNameEn = settings.LegalNameEn; order.InvoiceTaxRegistrationNumber = settings.TaxRegistrationNumber; order.InvoiceCommercialRegistrationNumber = settings.CommercialRegistrationNumber;
        order.InvoiceAddressAr = settings.AddressAr; order.InvoiceAddressEn = settings.AddressEn; order.InvoicePhone = settings.Phone; order.InvoiceFooter = settings.Footer;
    }

    public static void CalculateOrder(RestaurantOrder order)
    {
        if (order.InvoicePricesIncludeTax is null)
        {
            order.Subtotal = Round(order.Items.Where(x => !x.IsCancelled).Sum(x => x.LineTotal));
            order.DiscountAmount = Math.Min(Round(order.DiscountAmount), order.Subtotal);
            order.GrandTotal = Round(order.Subtotal - order.DiscountAmount);
            return;
        }
        var inclusive = order.InvoicePricesIncludeTax ?? false; var rate = order.InvoiceDefaultTaxRate ?? 0;
        foreach (var line in order.Items.Where(x => !x.IsCancelled)) CalculateLine(line, inclusive, rate);
        order.Subtotal = Round(order.Items.Where(x => !x.IsCancelled).Sum(x => x.LineTotal));
        order.DiscountAmount = Math.Min(Round(order.DiscountAmount), order.Subtotal);
        var afterDiscount = Round(order.Subtotal - order.DiscountAmount);
        var lineTax = Round(order.Items.Where(x => !x.IsCancelled).Sum(x => x.InvoiceTaxSnapshot ?? 0));
        var discountTax = inclusive ? 0 : Round(order.DiscountAmount * rate / 100m);
        order.GrandTotal = inclusive ? afterDiscount : Round(afterDiscount + Math.Max(0, lineTax - discountTax));
    }

    public static void CaptureCompletedSnapshot(RestaurantOrder order)
    {
        if (order.InvoiceSnapshotCapturedAt is not null) return;
        CalculateOrder(order);
        var totals = Totals(order);
        order.InvoiceSubtotalSnapshot = totals.Subtotal; order.InvoiceDiscountSnapshot = totals.Discount; order.InvoiceTaxSnapshot = totals.Tax; order.InvoiceGrandTotalSnapshot = totals.GrandTotal; order.InvoiceSnapshotCapturedAt = DateTime.UtcNow;
    }

    public async Task<InvoiceDocument> GetDocumentAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await db.RestaurantOrders.AsNoTracking().Include(x => x.Branch).Include(x => x.OrderType).Include(x => x.Table)
            .Include(x => x.Items).ThenInclude(x => x.Modifiers).ThenInclude(x => x.ModifierOption)
            .Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.ComboComponent)
            .Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.SelectedMenuItem)
            .Include(x => x.Payments).ThenInclude(x => x.PaymentMethod).FirstOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new NotFoundException("Order not found.");
        return BuildDocument(order);
    }

    public static InvoiceDocument BuildDocument(RestaurantOrder order)
    {
        var legacy = order.InvoicePricesIncludeTax is null; var inclusive = order.InvoicePricesIncludeTax ?? false; var rate = order.InvoiceDefaultTaxRate ?? 0;
        var lines = order.Items.Where(x => !x.IsCancelled).Select(line =>
        {
            var values = legacy ? (Net: Round(line.LineTotal), Tax: 0m, Gross: Round(line.LineTotal)) : LineValues(line, inclusive, rate);
            var details = line.Modifiers.Select(x => $"+ {x.ModifierOption.NameEn} ({x.PriceDeltaSnapshot:+0.000;-0.000;0.000})").Concat(line.ComboSelections.Select(x => $"{x.ComboComponent.SlotLabel}: {x.SelectedMenuItem.NameEn} ({x.PriceDeltaSnapshot:+0.000;-0.000;0.000})")).ToList();
            return new InvoiceLineDocument(line.Id, line.MenuItemNameSnapshot, line.Quantity, line.UnitPriceSnapshot, legacy ? 0 : line.InvoiceTaxRateSnapshot ?? rate, values.Net, values.Tax, values.Gross, line.Notes, details);
        }).ToList();
        var totals = order.InvoiceSnapshotCapturedAt is null ? Totals(order) : (order.InvoiceSubtotalSnapshot ?? 0, order.InvoiceDiscountSnapshot ?? 0, order.InvoiceTaxSnapshot ?? 0, order.InvoiceGrandTotalSnapshot ?? order.GrandTotal);
        var location = order.OrderType?.Code == "CAR_PICKUP" ? order.CarPlateNumber : order.Table?.Label;
        return new(order.Id, order.OrderNumber, order.CreatedAt, location, order.InvoiceCurrency ?? "OMR", inclusive, order.InvoiceLegalNameAr ?? order.Branch?.NameAr ?? string.Empty, order.InvoiceLegalNameEn ?? order.Branch?.NameEn ?? string.Empty, order.InvoiceTaxRegistrationNumber, order.InvoiceCommercialRegistrationNumber, order.InvoiceAddressAr, order.InvoiceAddressEn, order.InvoicePhone, order.InvoiceFooter, totals.Item1, totals.Item2, totals.Item3, totals.Item4, lines, order.Payments.OrderBy(x => x.CreatedAt).Select(x => new InvoicePaymentDocument(x.PaymentMethod.Code, x.PaymentMethod.NameAr, x.PaymentMethod.NameEn, x.Amount)).ToList());
    }

    public byte[] CreatePdf(InvoiceDocument invoice)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(32); page.DefaultTextStyle(x => x.FontSize(10));
            page.Header().Column(c => { c.Item().Text(invoice.LegalNameEn).Bold().FontSize(18); c.Item().Text(invoice.LegalNameAr); c.Item().Text($"Tax invoice #{invoice.InvoiceNumber}   {invoice.Date:yyyy-MM-dd HH:mm}"); });
            page.Content().PaddingVertical(16).Column(c =>
            {
                c.Spacing(6); c.Item().Text(string.Join(" | ", new[] { invoice.TaxRegistrationNumber is null ? null : $"Tax: {invoice.TaxRegistrationNumber}", invoice.CommercialRegistrationNumber is null ? null : $"CR: {invoice.CommercialRegistrationNumber}", invoice.Phone, invoice.Location is null ? null : $"Location: {invoice.Location}" }.Where(x => x is not null)));
                c.Item().Table(t => { t.ColumnsDefinition(x => { x.RelativeColumn(4); x.ConstantColumn(40); x.RelativeColumn(); x.RelativeColumn(); x.RelativeColumn(); }); t.Header(h => { h.Cell().Text("Item").Bold(); h.Cell().Text("Qty").Bold(); h.Cell().AlignRight().Text("Net").Bold(); h.Cell().AlignRight().Text("Tax").Bold(); h.Cell().AlignRight().Text("Gross").Bold(); }); foreach (var line in invoice.Lines) { t.Cell().Text(line.Name); t.Cell().Text(line.Quantity.ToString()); t.Cell().AlignRight().Text(line.Net.ToString("0.000")); t.Cell().AlignRight().Text(line.Tax.ToString("0.000")); t.Cell().AlignRight().Text(line.Gross.ToString("0.000")); } });
                c.Item().AlignRight().Column(x => { x.Item().Text($"Subtotal {invoice.Subtotal:0.000} {invoice.Currency}"); x.Item().Text($"Discount {invoice.Discount:0.000} {invoice.Currency}"); x.Item().Text($"Tax {invoice.Tax:0.000} {invoice.Currency}"); x.Item().Text($"Grand total {invoice.GrandTotal:0.000} {invoice.Currency}").Bold(); });
                if (invoice.Payments.Count > 0) c.Item().Text("Payments: " + string.Join(", ", invoice.Payments.Select(x => $"{x.NameEn} {x.Amount:0.000}")));
            });
            if (!string.IsNullOrWhiteSpace(invoice.Footer)) page.Footer().AlignCenter().Text(invoice.Footer);
        })).GeneratePdf();
    }

    private static void CalculateLine(RestaurantOrderItem line, bool inclusive, decimal rate)
    {
        var values = Amounts(Round(line.UnitPriceSnapshot * line.Quantity), inclusive, rate);
        line.LineTotal = Round(line.UnitPriceSnapshot * line.Quantity); line.InvoiceTaxRateSnapshot = rate; line.InvoiceNetSnapshot = values.Net; line.InvoiceTaxSnapshot = values.Tax; line.InvoiceGrossSnapshot = values.Gross;
    }
    private static (decimal Net, decimal Tax, decimal Gross) LineValues(RestaurantOrderItem line, bool inclusive, decimal rate) => line.InvoiceNetSnapshot.HasValue ? (line.InvoiceNetSnapshot.Value, line.InvoiceTaxSnapshot ?? 0, line.InvoiceGrossSnapshot ?? line.LineTotal) : Amounts(Round(line.LineTotal), inclusive, rate);
    private static (decimal Net, decimal Tax, decimal Gross) Amounts(decimal amount, bool inclusive, decimal rate) { if (inclusive) { var net = rate == 0 ? amount : Round(amount / (1 + rate / 100m)); return (net, Round(amount - net), amount); } var tax = Round(amount * rate / 100m); return (amount, tax, Round(amount + tax)); }
    private static (decimal Subtotal, decimal Discount, decimal Tax, decimal GrandTotal) Totals(RestaurantOrder order)
    {
        if (order.InvoicePricesIncludeTax is null) return (Round(order.Subtotal), Round(order.DiscountAmount), 0, Round(order.GrandTotal));
        var inclusive = order.InvoicePricesIncludeTax.Value; var rate = order.InvoiceDefaultTaxRate ?? 0; var active = order.Items.Where(x => !x.IsCancelled).ToList(); var sourceSubtotal = Round(active.Sum(x => x.LineTotal)); var sourceDiscount = Math.Min(Round(order.DiscountAmount), sourceSubtotal); var after = Round(sourceSubtotal - sourceDiscount);
        var lineTax = Round(active.Sum(x => LineValues(x, inclusive, rate).Tax));
        if (inclusive) { var netBefore = Round(active.Sum(x => LineValues(x, true, rate).Net)); var netDiscount = rate == 0 ? sourceDiscount : Round(sourceDiscount / (1 + rate / 100m)); var discountTax = Round(sourceDiscount - netDiscount); return (netBefore, netDiscount, Math.Max(0, Round(lineTax - discountTax)), after); }
        var tax = Math.Max(0, Round(lineTax - Round(sourceDiscount * rate / 100m))); return (sourceSubtotal, sourceDiscount, tax, Round(after + tax));
    }
}
