using POS.Application.Notifications;
using POS.Application.Sales;
using POS.Application.Inventory;
using Xunit;

namespace POS.Application.Tests;

public class LowStockTests
{
    [Fact]
    public void Sale_is_not_blocked_when_consumption_exceeds_stock()
    {
        Assert.Equal(0m, StockLevelCalculator.AfterSale(currentQuantity: 2m, consumedQuantity: 5m));
    }

    [Theory]
    [InlineData("طحين", 50, 5, 250)]
    [InlineData("حليب بودرة", 1.8, 18, 32.4)]
    [InlineData("حليب سائل", 72, 10, 720)]
    [InlineData("شاي أحمر", 50, 5, 250)]
    public void Package_receipts_are_converted_to_consumption_units(
        string material, decimal conversion, decimal packageCount, decimal expected)
    {
        Assert.False(string.IsNullOrWhiteSpace(material));
        Assert.Equal(expected, InventoryQuantityCalculator.FromPackages(conversion, packageCount));
    }

    [Fact]
    public void Alert_is_an_html_invoice_with_branch_item_current_and_replenishment()
    {
        var html = LowStockEmailTemplate.Build(new LowStockEmailData(
            "فرع السويق", "حليب", "لتر", 2m, 10m,
            new DateTime(2026, 8, 26, 8, 30, 0, DateTimeKind.Utc)));

        Assert.Contains("فرع السويق", html);
        Assert.Contains("حليب", html);
        Assert.Contains("2.000 لتر", html);
        Assert.Contains("8.000 لتر", html);
        Assert.Contains("dir=\"rtl\"", html);
    }
}
