namespace POS.Application.Shifts;

public static class ShiftCashCalculator
{
    public static decimal Expected(decimal openingCash, decimal inStoreCashSales) => openingCash + inStoreCashSales;
    public static decimal Actual(IEnumerable<CashCountLineRequest> counts) => counts.Sum(c => c.Denomination * c.Quantity);
    public static decimal Variance(decimal actual, decimal expected) => actual - expected;
}
