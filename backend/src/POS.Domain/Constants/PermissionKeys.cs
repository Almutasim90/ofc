namespace POS.Domain.Constants;

public static class PermissionKeys
{
    public const string SalesCreate = "sales.create";
    public const string SalesVoid = "sales.void";
    public const string InventoryAdjust = "inventory.adjust";
    public const string ReportsBranchView = "reports.branch.view";
    public const string ReportsGlobalView = "reports.global.view";
    public const string UsersManage = "users.manage";
    public const string BranchesManage = "branches.manage";
    public const string ClosingConfigure = "closing.configure";
    public const string ProductsManage = "products.manage";
    public const string ChannelsManage = "channels.manage";
    public const string AiManage = "ai.manage";
    public const string EmailManage = "email.manage";

    public static readonly IReadOnlyList<string> All =
    [
        SalesCreate,
        SalesVoid,
        InventoryAdjust,
        ReportsBranchView,
        ReportsGlobalView,
        UsersManage,
        BranchesManage,
        ClosingConfigure,
        ProductsManage,
        ChannelsManage,
        AiManage,
        EmailManage,
    ];
}
