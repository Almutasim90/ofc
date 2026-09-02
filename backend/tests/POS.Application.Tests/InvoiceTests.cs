using System.Text;
using POS.Application.Invoices;
using POS.Application.Printing;
using POS.Domain.Entities;
using Xunit;

namespace POS.Application.Tests;

public class InvoiceTests
{
    [Fact]
    public void Exclusive_and_inclusive_tax_use_three_decimal_rounding()
    {
        var exclusive = Order(10m);
        InvoiceService.ApplySettings(exclusive, Settings(false, 5));
        InvoiceService.CalculateOrder(exclusive);
        Assert.Equal(10.500m, exclusive.GrandTotal);
        Assert.Equal(0.500m, exclusive.Items.Single().InvoiceTaxSnapshot);

        var inclusive = Order(10m);
        InvoiceService.ApplySettings(inclusive, Settings(true, 5));
        InvoiceService.CalculateOrder(inclusive);
        Assert.Equal(10.000m, inclusive.GrandTotal);
        Assert.Equal(0.476m, inclusive.Items.Single().InvoiceTaxSnapshot);
        Assert.Equal(9.524m, inclusive.Items.Single().InvoiceNetSnapshot);
    }

    [Fact]
    public void Completed_snapshot_pdf_and_escpos_are_stable_and_structured()
    {
        var order = Order(12m);
        InvoiceService.ApplySettings(order, Settings(false, 5));
        InvoiceService.CalculateOrder(order);
        InvoiceService.CaptureCompletedSnapshot(order);
        var originalTotal = order.InvoiceGrandTotalSnapshot;
        order.Items.Single().LineTotal = 999;
        InvoiceService.ApplySettings(order, Settings(false, 20));

        var document = InvoiceService.BuildDocument(order);
        var pdf = new InvoiceService(null!).CreatePdf(document);
        var receipt = Encoding.UTF8.GetString(OrderPrintingService.BuildCustomerReceipt(document));

        Assert.Equal(originalTotal, document.GrandTotal);
        Assert.Equal(5, document.Lines.Single().TaxRate);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.Contains("TAX REG OM123", receipt);
        Assert.Contains("GRAND TOTAL 12.600 OMR", receipt);
        Assert.Equal(0x1D, OrderPrintingService.BuildCustomerReceipt(document)[^3]);
    }

    [Fact]
    public void Invoice_tax_total_matches_the_sum_of_rounded_lines()
    {
        var order = Order(0.010m);
        order.Items.Add(new RestaurantOrderItem { Id = Guid.NewGuid(), Order = order, MenuItemNameSnapshot = "Water", UnitPriceSnapshot = 0.010m, Quantity = 1, LineTotal = 0.010m });
        InvoiceService.ApplySettings(order, Settings(false, 5));

        InvoiceService.CalculateOrder(order);
        var document = InvoiceService.BuildDocument(order);

        Assert.Equal(document.Lines.Sum(x => x.Tax), document.Tax);
        Assert.Equal(0.022m, document.GrandTotal);
    }

    private static RestaurantOrder Order(decimal price)
    {
        var branch = new Branch { Id = Guid.NewGuid(), NameAr = "شركة", NameEn = "Company" };
        var type = new OrderType { Id = Guid.NewGuid(), Code = "TAKEAWAY", NameAr = "سفري", NameEn = "Takeaway" };
        var order = new RestaurantOrder { Id = Guid.NewGuid(), BranchId = branch.Id, Branch = branch, OrderNumber = 42, OrderType = type, OrderTypeId = type.Id, CreatedAt = DateTime.UtcNow };
        order.Items.Add(new RestaurantOrderItem { Id = Guid.NewGuid(), Order = order, MenuItemNameSnapshot = "Tea", UnitPriceSnapshot = price, Quantity = 1, LineTotal = price });
        return order;
    }

    private static InvoiceSettingsDto Settings(bool inclusive, decimal rate) => new(Guid.NewGuid(), "شركة", "Company", "OM123", "CR456", "مسقط", "Muscat", "1234", "OMR", inclusive, rate, "Thank you");
}
