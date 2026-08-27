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
}
