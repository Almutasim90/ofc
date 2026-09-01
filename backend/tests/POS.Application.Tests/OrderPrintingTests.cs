using System.Text;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Printing;
using POS.Application.RestaurantInventory;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
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

    [Fact]
    public void Car_plate_is_printed_on_kitchen_ticket_and_receipt()
    {
        var order = Order(("Burger", null));
        order.OrderType.Code = "CAR_PICKUP";
        order.CarPlateNumber = "OMAN 1234";

        var jobs = OrderPrintingService.BuildJobs(order, Printer("127.0.0.1"));

        Assert.All(jobs, job => Assert.Contains("LOCATION OMAN 1234", Encoding.UTF8.GetString(job.Payload)));
    }

    [Fact]
    public async Task Confirmation_consumes_recipe_stock_before_printing()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated();
        var branchId = Guid.NewGuid();
        var category = new MenuCategory { Id = Guid.NewGuid(), NameAr = "وجبات", NameEn = "Meals" };
        var item = new MenuItem { Id = Guid.NewGuid(), Category = category, NameAr = "برجر", NameEn = "Burger", BasePrice = 3 };
        var unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = $"Unit-{Guid.NewGuid()}", Symbol = "kg", IsBase = true };
        var ingredient = new Ingredient { Id = Guid.NewGuid(), NameAr = "دقيق", NameEn = "Flour", UnitOfMeasure = unit };
        var warehouse = new Warehouse { Id = Guid.NewGuid(), BranchId = branchId, NameAr = "رئيسي", NameEn = "Main", IsDefault = true };
        var stock = new WarehouseIngredientStock { Warehouse = warehouse, Ingredient = ingredient, CurrentQuantity = 10 };
        var type = await db.OrderTypes.SingleAsync(x => x.Code == "TAKEAWAY", TestContext.Current.CancellationToken);
        var order = new RestaurantOrder { Id = Guid.NewGuid(), BranchId = branchId, OrderNumber = 42, OrderType = type, Status = RestaurantOrderStatuses.Open };
        order.Items.Add(new RestaurantOrderItem { Id = Guid.NewGuid(), MenuItem = item, MenuItemNameSnapshot = item.NameEn, Quantity = 2, UnitPriceSnapshot = 3, LineTotal = 6 });
        db.AddRange(category, item, unit, ingredient, warehouse, stock, order,
            new MenuItemRecipeLine { Id = Guid.NewGuid(), MenuItem = item, BranchId = branchId, Ingredient = ingredient, QuantityRequired = 1 },
            new PrinterConfig { Id = Guid.NewGuid(), BranchId = branchId, NameAr = "فاتورة", NameEn = "Receipt", IpAddress = "127.0.0.1", Port = 9100, IsDefault = true, IsActive = true });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var printer = new RecordingPrinter();
        var inventory = new RestaurantInventoryService(db, new User());
        var service = new OrderPrintingService(db, printer, inventory);

        await service.ConfirmAndPrintAsync(order.Id, TestContext.Current.CancellationToken);

        Assert.Equal(RestaurantOrderStatuses.Sent, order.Status);
        Assert.Equal(8, stock.CurrentQuantity);
        Assert.Equal(-2, Assert.Single(db.RestaurantInventoryTransactions).QuantityChange);
        Assert.Equal(2, printer.Jobs.Count);
    }

    private static PrinterConfig Printer(string ip) => new() { Id = Guid.NewGuid(), IpAddress = ip, Port = 9100, IsActive = true };
    private static PrinterSection Section(string name, PrinterConfig printer) => new() { Id = Guid.NewGuid(), NameEn = name, NameAr = name, PrinterConfig = printer, PrinterConfigId = printer.Id };
    private static RestaurantOrder Order(params (string Name, PrinterSection? Section)[] lines)
    {
        var order = new RestaurantOrder { Id = Guid.NewGuid(), OrderNumber = 42, GrandTotal = lines.Length, OrderType = new OrderType { Code = "TAKEAWAY" } };
        foreach (var line in lines) order.Items.Add(new RestaurantOrderItem { Id = Guid.NewGuid(), MenuItemNameSnapshot = line.Name, Quantity = 1, LineTotal = 1, MenuItem = new MenuItem { Id = Guid.NewGuid(), PrinterSection = line.Section, PrinterSectionId = line.Section?.Id } });
        return order;
    }

    private sealed class RecordingPrinter : IRawPrinterClient
    {
        public List<byte[]> Jobs { get; } = [];
        public Task SendAsync(string ipAddress, int port, byte[] payload, CancellationToken cancellationToken = default) { Jobs.Add(payload); return Task.CompletedTask; }
    }

    private sealed class User : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public Guid? BranchId => null;
        public string? RoleName => null;
        public IReadOnlyCollection<string> Permissions => [];
        public bool BypassBranchFilter => true;
    }
}
