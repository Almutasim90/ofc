namespace POS.Domain.Constants;

public static class DefaultRolePermissions
{
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> ByRole =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [RoleNames.Cashier] =
            [
                PermissionKeys.SalesCreate,
                PermissionKeys.SalesEdit,
            ],
            [RoleNames.BranchManager] =
            [
                PermissionKeys.SalesCreate,
                PermissionKeys.SalesEdit,
                PermissionKeys.SalesVoid,
                PermissionKeys.InventoryAdjust,
                PermissionKeys.ReportsBranchView,
                PermissionKeys.UsersManage,
                PermissionKeys.OrdersCreate,
                PermissionKeys.OrdersCancel,
                PermissionKeys.OrdersTransfer,
                PermissionKeys.TablesManage,
                PermissionKeys.ProductsManage,
                PermissionKeys.CombosManage,
                PermissionKeys.ModifiersManage,
                PermissionKeys.PrintingManage,
            ],
            [RoleNames.GeneralManager] = PermissionKeys.All,
        };
}
