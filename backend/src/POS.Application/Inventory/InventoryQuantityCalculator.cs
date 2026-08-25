namespace POS.Application.Inventory;

public static class InventoryQuantityCalculator
{
    public static decimal FromPackages(decimal baseQuantityPerPackage, decimal packageCount)
    {
        if (baseQuantityPerPackage <= 0m) throw new ArgumentOutOfRangeException(nameof(baseQuantityPerPackage));
        if (packageCount < 0m) throw new ArgumentOutOfRangeException(nameof(packageCount));
        return decimal.Round(baseQuantityPerPackage * packageCount, 3);
    }
}
