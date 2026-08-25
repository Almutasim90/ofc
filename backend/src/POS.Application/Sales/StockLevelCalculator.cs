namespace POS.Application.Sales;

public static class StockLevelCalculator
{
    public static decimal AfterSale(decimal currentQuantity, decimal consumedQuantity) =>
        Math.Max(0m, currentQuantity - consumedQuantity);
}
