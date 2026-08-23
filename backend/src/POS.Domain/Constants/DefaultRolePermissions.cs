namespace POS.Domain.Constants;

public static class DefaultRolePermissions
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByRole =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [RoleNames.Cashier] =
            [
                PermissionKeys.SalesCreate,
            ],
            [RoleNames.BranchManager] =
            [
                PermissionKeys.SalesCreate,
                PermissionKeys.SalesVoid,
                PermissionKeys.InventoryAdjust,
                PermissionKeys.ReportsBranchView,
                PermissionKeys.UsersManage,
            ],
            [RoleNames.GeneralManager] = PermissionKeys.All,
        };
}
