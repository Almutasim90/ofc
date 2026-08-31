using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;

namespace POS.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserPermissionOverride> UserPermissionOverrides { get; }

    DbSet<Branch> Branches { get; }
    DbSet<RestaurantTable> RestaurantTables { get; }
    DbSet<BranchFeatureFlag> BranchFeatureFlags { get; }
    DbSet<MenuCategory> MenuCategories { get; }
    DbSet<CategoryBranchAvailability> CategoryBranchAvailabilities { get; }
    DbSet<MenuItem> MenuItems { get; }
    DbSet<ComboComponent> ComboComponents { get; }
    DbSet<ComboComponentOption> ComboComponentOptions { get; }
    DbSet<ModifierGroup> ModifierGroups { get; }
    DbSet<ModifierOption> ModifierOptions { get; }
    DbSet<MenuItemModifierGroup> MenuItemModifierGroups { get; }
    DbSet<OrderType> OrderTypes { get; }
    DbSet<RestaurantOrder> RestaurantOrders { get; }
    DbSet<RestaurantOrderItem> RestaurantOrderItems { get; }
    DbSet<OrderItemComboSelection> OrderItemComboSelections { get; }
    DbSet<OrderItemModifier> OrderItemModifiers { get; }
    DbSet<OrderCancellation> OrderCancellations { get; }
    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<Ingredient> Ingredients { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<WarehouseIngredientStock> WarehouseIngredientStocks { get; }
    DbSet<MenuItemRecipeLine> MenuItemRecipeLines { get; }
    DbSet<InventoryTransactionReason> InventoryTransactionReasons { get; }
    DbSet<RestaurantInventoryTransaction> RestaurantInventoryTransactions { get; }
    DbSet<StockCount> StockCounts { get; }
    DbSet<StockCountLine> StockCountLines { get; }
    DbSet<PrinterConfig> PrinterConfigs { get; }
    DbSet<PrinterSection> PrinterSections { get; }
    DbSet<PaymentMethod> PaymentMethods { get; }
    DbSet<OrderPayment> OrderPayments { get; }
    DbSet<OrderEditLog> OrderEditLogs { get; }
    DbSet<CashShift> CashShifts { get; }
    DbSet<CashCount> CashCounts { get; }
    DbSet<BranchSalesChannelAvailability> BranchSalesChannelAvailabilities { get; }
    DbSet<Product> Products { get; }
    DbSet<SalesChannel> SalesChannels { get; }
    DbSet<ProductChannelPrice> ProductChannelPrices { get; }
    DbSet<RawMaterial> RawMaterials { get; }
    DbSet<BranchRawMaterialStock> BranchRawMaterialStocks { get; }
    DbSet<ProductRecipe> ProductRecipes { get; }
    DbSet<StockAdjustment> StockAdjustments { get; }
    DbSet<SupplyPackage> SupplyPackages { get; }
    DbSet<StockReceipt> StockReceipts { get; }

    DbSet<SaleEdit> SaleEdits { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleItem> SaleItems { get; }
    DbSet<SaleInventoryConsumption> SaleInventoryConsumptions { get; }
    DbSet<Shift> Shifts { get; }
    DbSet<ShiftCashCount> ShiftCashCounts { get; }
    DbSet<VoidRequest> VoidRequests { get; }
    DbSet<ClosingScheduleConfig> ClosingScheduleConfigs { get; }
    DbSet<ClosingScheduleException> ClosingScheduleExceptions { get; }
    DbSet<LowStockNotification> LowStockNotifications { get; }
    DbSet<AiProviderSetting> AiProviderSettings { get; }
    DbSet<AiInsightRequest> AiInsightRequests { get; }
    DbSet<EmailSettings> EmailSettings { get; }
    DbSet<ReceiptSettings> ReceiptSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> ClaimNextSaleNumberAsync(Guid branchId, CancellationToken cancellationToken = default);
    Task<int> ClaimNextOrderNumberAsync(Guid branchId, CancellationToken cancellationToken = default);
}
