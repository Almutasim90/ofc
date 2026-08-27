using POS.Application.Common;
using POS.Domain.Constants;

namespace POS.Application.Sales;

public static class SalePaymentCalculator
{
    public static (decimal Cash, decimal Card) Calculate(string method, decimal total, decimal? cash, decimal? card)
    {
        if (method == PaymentMethods.Cash && cash is null && card is null) return (total, 0);
        if (method == PaymentMethods.Card && cash is null && card is null) return (0, total);
        if (!PaymentMethods.All.Contains(method) || cash is null || card is null
            || cash < 0 || card < 0 || decimal.Round(cash.Value, 3) != cash || decimal.Round(card.Value, 3) != card
            || cash + card != total || (method == PaymentMethods.Cash && card != 0)
            || (method == PaymentMethods.Card && cash != 0)
            || (method == PaymentMethods.Mixed && (cash <= 0 || card <= 0)))
            throw new ValidationException("Payment amounts must match the total; mixed payment requires positive cash and card amounts.");
        return (cash.Value, card.Value);
    }
}
