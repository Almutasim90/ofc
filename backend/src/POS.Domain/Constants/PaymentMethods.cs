namespace POS.Domain.Constants;

public static class PaymentMethods
{
    public const string Cash = "Cash";
    public const string Card = "Card";

    public static readonly IReadOnlyList<string> All = [Cash, Card];
}
