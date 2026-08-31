using System.Text;
using POS.Application.Printing;
using POS.Domain.Entities;
using Xunit;

namespace POS.Application.Tests;

public class OrderPrintingTests
{
    [Fact]
    public void Hot_and_drink_items_route_to_two_printers_plus_one_receipt()
    {
        var receipt = Printer("10.0.0.10"); var hot = Section("Hot", Printer("10.0.0.11")); var drinks = Section("Drinks", Printer("10.0.0.12"));
        var order = Order(("Burger", hot), ("Cola", drinks));
        var jobs = OrderPrintingService.BuildJobs(order, receipt);
        Assert.Equal(3, jobs.Count); Assert.Equal(2, jobs.Count(x => x.Kind == "Kitchen")); Assert.Single(jobs, x => x.Kind == "Receipt");
        Assert.Contains(jobs, x => x.IpAddress == "10.0.0.11" && Encoding.UTF8.GetString(x.Payload).Contains("Burger"));
        Assert.Contains(jobs, x => x.IpAddress == "10.0.0.12" && Encoding.UTF8.GetString(x.Payload).Contains("Cola"));
    }

    [Fact]
    public void Unassigned_items_use_default_printer() => Assert.All(OrderPrintingService.BuildJobs(Order(("Item", null)), Printer("127.0.0.1")), x => Assert.Equal("127.0.0.1", x.IpAddress));

    [Fact]
    public void Esc_pos_document_initializes_and_cuts_paper()
    {
        var bytes = EscPosDocument.Text("test"); Assert.Equal([0x1B, 0x40], bytes[..2]); Assert.Equal([0x1D, 0x56, 0x00], bytes[^3..]);
    }

    private static PrinterConfig Printer(string ip) => new() { Id = Guid.NewGuid(), IpAddress = ip, Port = 9100, IsActive = true };
    private static PrinterSection Section(string name, PrinterConfig printer) => new() { Id = Guid.NewGuid(), NameEn = name, NameAr = name, PrinterConfig = printer, PrinterConfigId = printer.Id };
    private static RestaurantOrder Order(params (string Name, PrinterSection? Section)[] lines)
    {
        var order = new RestaurantOrder { Id = Guid.NewGuid(), OrderNumber = 42, GrandTotal = lines.Length, OrderType = new OrderType { Code = "TAKEAWAY" } };
        foreach (var line in lines) order.Items.Add(new RestaurantOrderItem { Id = Guid.NewGuid(), MenuItemNameSnapshot = line.Name, Quantity = 1, LineTotal = 1, MenuItem = new MenuItem { Id = Guid.NewGuid(), PrinterSection = line.Section, PrinterSectionId = line.Section?.Id } });
        return order;
    }
}
