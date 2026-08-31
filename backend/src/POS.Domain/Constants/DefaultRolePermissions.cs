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
                PermissionKeys.OrdersCreate,
                PermissionKeys.OrdersCancel,
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
                PermissionKeys.OrdersEditClosed,
                PermissionKeys.PaymentsApproveDebt,
                PermissionKeys.CombosManage,
                PermissionKeys.ModifiersManage,
                PermissionKeys.TablesManage,
                PermissionKeys.PrintingManage,
            ],
            [RoleNames.GeneralManager] = PermissionKeys.All,
        };
}
