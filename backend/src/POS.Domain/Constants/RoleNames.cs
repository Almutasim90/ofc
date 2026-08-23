namespace POS.Domain.Constants;

public static class RoleNames
{
    public const string Cashier = "Cashier";
    public const string BranchManager = "BranchManager";
    public const string GeneralManager = "GeneralManager";

    public static readonly IReadOnlyList<string> All =
    [
        Cashier,
        BranchManager,
        GeneralManager,
    ];
}
