using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUserService? _currentUser;
    private bool BypassRestaurantBranchFilter => _currentUser?.BypassBranchFilter ?? true;
    private Guid? RestaurantBranchId => _currentUser?.BranchId;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();

    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
    public DbSet<BranchFeatureFlag> BranchFeatureFlags => Set<BranchFeatureFlag>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<CategoryBranchAvailability> CategoryBranchAvailabilities => Set<CategoryBranchAvailability>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<ComboComponent> ComboComponents => Set<ComboComponent>();
    public DbSet<ComboComponentOption> ComboComponentOptions => Set<ComboComponentOption>();
    public DbSet<ModifierGroup> ModifierGroups => Set<ModifierGroup>();
    public DbSet<ModifierOption> ModifierOptions => Set<ModifierOption>();
    public DbSet<MenuItemModifierGroup> MenuItemModifierGroups => Set<MenuItemModifierGroup>();
    public DbSet<OrderType> OrderTypes => Set<OrderType>();
    public DbSet<RestaurantOrder> RestaurantOrders => Set<RestaurantOrder>();
    public DbSet<RestaurantOrderItem> RestaurantOrderItems => Set<RestaurantOrderItem>();
    public DbSet<OrderItemComboSelection> OrderItemComboSelections => Set<OrderItemComboSelection>();
    public DbSet<OrderItemModifier> OrderItemModifiers => Set<OrderItemModifier>();
    public DbSet<OrderCancellation> OrderCancellations => Set<OrderCancellation>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure=>Set<UnitOfMeasure>();public DbSet<Ingredient> Ingredients=>Set<Ingredient>();public DbSet<Warehouse> Warehouses=>Set<Warehouse>();public DbSet<WarehouseIngredientStock> WarehouseIngredientStocks=>Set<WarehouseIngredientStock>();public DbSet<MenuItemRecipeLine> MenuItemRecipeLines=>Set<MenuItemRecipeLine>();public DbSet<InventoryTransactionReason> InventoryTransactionReasons=>Set<InventoryTransactionReason>();public DbSet<RestaurantInventoryTransaction> RestaurantInventoryTransactions=>Set<RestaurantInventoryTransaction>();
    public DbSet<StockCount> StockCounts=>Set<StockCount>();public DbSet<StockCountLine> StockCountLines=>Set<StockCountLine>();
    public DbSet<PrinterConfig> PrinterConfigs => Set<PrinterConfig>();
    public DbSet<PrinterSection> PrinterSections => Set<PrinterSection>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>(); public DbSet<OrderPayment> OrderPayments => Set<OrderPayment>(); public DbSet<OrderEditLog> OrderEditLogs => Set<OrderEditLog>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SalesChannel> SalesChannels => Set<SalesChannel>();
    public DbSet<ProductChannelPrice> ProductChannelPrices => Set<ProductChannelPrice>();
    public DbSet<RawMaterial> RawMaterials => Set<RawMaterial>();
    public DbSet<BranchRawMaterialStock> BranchRawMaterialStocks => Set<BranchRawMaterialStock>();
    public DbSet<ProductRecipe> ProductRecipes => Set<ProductRecipe>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<SupplyPackage> SupplyPackages => Set<SupplyPackage>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();

    public DbSet<SaleEdit> SaleEdits => Set<SaleEdit>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SaleInventoryConsumption> SaleInventoryConsumptions => Set<SaleInventoryConsumption>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftCashCount> ShiftCashCounts => Set<ShiftCashCount>();
    public DbSet<VoidRequest> VoidRequests => Set<VoidRequest>();
    public DbSet<ClosingScheduleConfig> ClosingScheduleConfigs => Set<ClosingScheduleConfig>();
    public DbSet<ClosingScheduleException> ClosingScheduleExceptions => Set<ClosingScheduleException>();
    public DbSet<LowStockNotification> LowStockNotifications => Set<LowStockNotification>();
    public DbSet<AiProviderSetting> AiProviderSettings => Set<AiProviderSetting>();
    public DbSet<AiInsightRequest> AiInsightRequests => Set<AiInsightRequest>();
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();
    public DbSet<ReceiptSettings> ReceiptSettings => Set<ReceiptSettings>();

    // A single UPDATE ... RETURNING is one atomic statement in Postgres: concurrent callers
    // for the same branch serialize on the row lock and each gets a distinct number, so this
    // never needs an explicit transaction or a retry loop.
    public async Task<int> ClaimNextSaleNumberAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var claimed = await Database.SqlQuery<int>(
            $"""UPDATE "Branches" SET "NextSaleNumber" = "NextSaleNumber" + 1 WHERE "Id" = {branchId} RETURNING "NextSaleNumber" - 1 AS "Value" """)
            .ToListAsync(cancellationToken);
        return claimed.Count > 0
            ? claimed[0]
            : throw new InvalidOperationException($"Branch {branchId} was not found while claiming a sale number.");
    }

    public async Task<int> ClaimNextOrderNumberAsync(Guid branchId, CancellationToken cancellationToken = default)
    {
        var claimed = await Database.SqlQuery<int>($"""UPDATE "Branches" SET "NextOrderNumber" = "NextOrderNumber" + 1 WHERE "Id" = {branchId} RETURNING "NextOrderNumber" - 1 AS "Value" """).ToListAsync(cancellationToken);
        return claimed.Count > 0 ? claimed[0] : throw new InvalidOperationException($"Branch {branchId} was not found while claiming an order number.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(r => r.Name).IsUnique();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Key).IsRequired().HasMaxLength(100);
            entity.HasIndex(p => p.Key).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            entity.HasOne(rp => rp.Role).WithMany(r => r.RolePermissions).HasForeignKey(rp => rp.RoleId);
            entity.HasOne(rp => rp.Permission).WithMany().HasForeignKey(rp => rp.PermissionId);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.FullName).IsRequired().HasMaxLength(200);
            entity.Property(u => u.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.Property(u => u.PreferredLanguage).IsRequired().HasMaxLength(5);
            entity.Property(u => u.PreferredTheme).HasMaxLength(5);
            entity.HasOne(u => u.Role).WithMany().HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(u => u.Branch).WithMany().HasForeignKey(u => u.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(u => u.BranchId);

            // Branch isolation: non-global users only ever see users in their own branch.
            entity.HasQueryFilter(u =>
                _currentUser == null
                || _currentUser.BypassBranchFilter
                || u.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<UserPermissionOverride>(entity =>
        {
            entity.HasKey(o => new { o.UserId, o.PermissionId });
            entity.HasOne(o => o.User).WithMany(u => u.PermissionOverrides).HasForeignKey(o => o.UserId);
            entity.HasOne(o => o.Permission).WithMany().HasForeignKey(o => o.PermissionId);

            // Mirrors User's branch filter so Include(o => o.User) can't leak cross-branch overrides.
            entity.HasQueryFilter(o =>
                _currentUser == null
                || _currentUser.BypassBranchFilter
                || o.User.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(b => b.Id);
            entity.Property(b => b.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(b => b.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(b => b.Code).IsRequired().HasMaxLength(50);
            entity.HasIndex(b => b.Code).IsUnique();
            entity.Property(b => b.DefaultOpeningFloat).HasPrecision(18, 3);
        });

        modelBuilder.Entity<RestaurantTable>(entity =>
        {
            entity.ToTable("Tables");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Label).IsRequired().HasMaxLength(100);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.BranchId, x.Label }).IsUnique();
            entity.HasQueryFilter(x => BypassRestaurantBranchFilter || x.BranchId == RestaurantBranchId);
        });

        modelBuilder.Entity<BranchFeatureFlag>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FeatureKey).IsRequired().HasMaxLength(100);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.BranchId, x.FeatureKey }).IsUnique();
            entity.HasQueryFilter(x => BypassRestaurantBranchFilter || x.BranchId == RestaurantBranchId);
        });

        modelBuilder.Entity<MenuCategory>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(x => x.NameEn).IsRequired().HasMaxLength(200);
            entity.HasIndex(x => x.SortOrder);
        });

        modelBuilder.Entity<CategoryBranchAvailability>(entity =>
        {
            entity.ToTable("CategoryBranchAvailability");
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.CategoryId, x.BranchId }).IsUnique();
            entity.HasIndex(x => x.BranchId);
            entity.HasQueryFilter(x => BypassRestaurantBranchFilter || x.BranchId == RestaurantBranchId);
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(x => x.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Kind).IsRequired().HasMaxLength(20);
            entity.Property(x => x.BasePrice).HasPrecision(12, 3);
            entity.Property(x => x.ImageUrl).HasMaxLength(1000);
            entity.HasOne(x => x.Category).WithMany(x => x.Items).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PrinterSection).WithMany().HasForeignKey(x => x.PrinterSectionId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.CategoryId, x.SortOrder });
            entity.ToTable(x => x.HasCheckConstraint("CK_MenuItems_Kind", "\"Kind\" IN ('SingleProduct','Combo')"));
        });

        modelBuilder.Entity<PrinterConfig>(entity =>
        {
            entity.HasKey(x => x.Id); entity.Property(x => x.NameAr).IsRequired().HasMaxLength(200); entity.Property(x => x.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(x => x.IpAddress).IsRequired().HasMaxLength(255); entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.BranchId, x.NameEn }).IsUnique(); entity.HasIndex(x => x.BranchId).IsUnique().HasFilter("\"IsDefault\" = TRUE");
            entity.ToTable(x => x.HasCheckConstraint("CK_PrinterConfigs_Port", "\"Port\" BETWEEN 1 AND 65535"));
            entity.HasQueryFilter(x => BypassRestaurantBranchFilter || x.BranchId == RestaurantBranchId);
        });
        modelBuilder.Entity<PrinterSection>(entity =>
        {
            entity.HasKey(x => x.Id); entity.Property(x => x.NameAr).IsRequired().HasMaxLength(200); entity.Property(x => x.NameEn).IsRequired().HasMaxLength(200);
            entity.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(x => x.PrinterConfig).WithMany().HasForeignKey(x => x.PrinterConfigId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.BranchId, x.NameEn }).IsUnique(); entity.HasIndex(x => x.PrinterConfigId);
            entity.HasQueryFilter(x => BypassRestaurantBranchFilter || x.BranchId == RestaurantBranchId);
        });
        modelBuilder.Entity<PaymentMethod>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Code).IsRequired().HasMaxLength(30);e.Property(x=>x.NameAr).IsRequired().HasMaxLength(100);e.Property(x=>x.NameEn).IsRequired().HasMaxLength(100);e.HasIndex(x=>x.Code).IsUnique();e.HasData(new PaymentMethod{Id=Guid.Parse("40000000-0000-0000-0000-000000000001"),Code="CASH",NameAr="نقدي",NameEn="Cash"},new PaymentMethod{Id=Guid.Parse("40000000-0000-0000-0000-000000000002"),Code="CARD",NameAr="بطاقة",NameEn="Card"},new PaymentMethod{Id=Guid.Parse("40000000-0000-0000-0000-000000000003"),Code="DEBT",NameAr="دَين",NameEn="Debt",RequiresApproval=true});});
        modelBuilder.Entity<OrderPayment>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Amount).HasPrecision(12,3);e.ToTable(x=>x.HasCheckConstraint("CK_OrderPayments_Amount","\"Amount\" > 0"));e.HasOne(x=>x.Order).WithMany(x=>x.Payments).HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Cascade);e.HasOne(x=>x.PaymentMethod).WithMany().HasForeignKey(x=>x.PaymentMethodId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.OrderId,x.CreatedAt});e.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.Order.BranchId==RestaurantBranchId);});
        modelBuilder.Entity<OrderEditLog>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.EditType).IsRequired().HasMaxLength(30);e.Property(x=>x.Notes).HasMaxLength(500);e.Property(x=>x.AmountDelta).HasPrecision(12,3);e.HasOne(x=>x.Order).WithMany(x=>x.EditLogs).HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Cascade);e.HasIndex(x=>new{x.OrderId,x.CreatedAt});e.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.Order.BranchId==RestaurantBranchId);});

        modelBuilder.Entity<ComboComponent>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SlotLabel).IsRequired().HasMaxLength(200);
            entity.HasOne(x => x.ComboMenuItem).WithMany(x => x.ComboComponents).HasForeignKey(x => x.ComboMenuItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ComboMenuItemId, x.SortOrder });
            entity.ToTable(x => x.HasCheckConstraint("CK_ComboComponents_Selection", "\"MinSelect\" >= 0 AND \"MaxSelect\" >= \"MinSelect\""));
        });

        modelBuilder.Entity<ComboComponentOption>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PriceDelta).HasPrecision(12, 3);
            entity.HasOne(x => x.ComboComponent).WithMany(x => x.Options).HasForeignKey(x => x.ComboComponentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MenuItem).WithMany().HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.ComboComponentId, x.MenuItemId }).IsUnique();
            entity.HasIndex(x => x.MenuItemId);
            entity.HasIndex(x => x.ComboComponentId).IsUnique().HasFilter("\"IsDefault\" = TRUE");
        });

        modelBuilder.Entity<ModifierGroup>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(x => x.NameEn).IsRequired().HasMaxLength(200);
            entity.ToTable(x => x.HasCheckConstraint("CK_ModifierGroups_Selection", "\"MinSelect\" >= 0 AND \"MaxSelect\" >= \"MinSelect\""));
        });

        modelBuilder.Entity<ModifierOption>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(x => x.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(x => x.PriceDelta).HasPrecision(12, 3);
            entity.HasOne(x => x.ModifierGroup).WithMany(x => x.Options).HasForeignKey(x => x.ModifierGroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.ModifierGroupId, x.NameEn }).IsUnique();
        });

        modelBuilder.Entity<MenuItemModifierGroup>(entity =>
        {
            entity.HasKey(x => new { x.MenuItemId, x.ModifierGroupId });
            entity.HasOne(x => x.MenuItem).WithMany(x => x.ModifierGroups).HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ModifierGroup).WithMany(x => x.MenuItems).HasForeignKey(x => x.ModifierGroupId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.ModifierGroupId);
        });

        modelBuilder.Entity<OrderType>(entity => { entity.HasKey(x=>x.Id); entity.Property(x=>x.Code).IsRequired().HasMaxLength(30); entity.HasIndex(x=>x.Code).IsUnique(); entity.Property(x=>x.NameAr).IsRequired().HasMaxLength(100); entity.Property(x=>x.NameEn).IsRequired().HasMaxLength(100); entity.HasData(
            new OrderType{Id=Guid.Parse("10000000-0000-0000-0000-000000000001"),Code="DINE_IN",NameAr="محلي",NameEn="Dine in"},
            new OrderType{Id=Guid.Parse("10000000-0000-0000-0000-000000000002"),Code="TAKEAWAY",NameAr="سفري",NameEn="Takeaway"},
            new OrderType{Id=Guid.Parse("10000000-0000-0000-0000-000000000003"),Code="CAR_PICKUP",NameAr="استلام سيارة",NameEn="Car pickup"},
            new OrderType{Id=Guid.Parse("10000000-0000-0000-0000-000000000004"),Code="DELIVERY",NameAr="توصيل",NameEn="Delivery"}); });
        modelBuilder.Entity<RestaurantOrder>(entity =>
        {
            entity.ToTable("Orders", x=>x.HasCheckConstraint("CK_Orders_Status", "\"Status\" IN ('Open','Sent','Paid','Closed','Cancelled')")); entity.HasKey(x=>x.Id);
            entity.Property(x=>x.Subtotal).HasPrecision(12,3); entity.Property(x=>x.DiscountAmount).HasPrecision(12,3); entity.Property(x=>x.GrandTotal).HasPrecision(12,3); entity.Property(x=>x.Status).IsRequired().HasMaxLength(20); entity.Property(x=>x.CarPlateNumber).HasMaxLength(30);
            entity.HasOne(x=>x.Branch).WithMany().HasForeignKey(x=>x.BranchId).OnDelete(DeleteBehavior.Restrict); entity.HasOne(x=>x.OrderType).WithMany().HasForeignKey(x=>x.OrderTypeId).OnDelete(DeleteBehavior.Restrict); entity.HasOne(x=>x.Table).WithMany().HasForeignKey(x=>x.TableId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x=>new{x.BranchId,x.OrderNumber}).IsUnique(); entity.HasIndex(x=>new{x.BranchId,x.BusinessDate,x.Status}); entity.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.BranchId==RestaurantBranchId);
        });
        modelBuilder.Entity<RestaurantOrderItem>(entity =>
        {
            entity.ToTable("OrderItems"); entity.HasKey(x=>x.Id); entity.Property(x=>x.MenuItemNameSnapshot).IsRequired().HasMaxLength(200); entity.Property(x=>x.UnitPriceSnapshot).HasPrecision(12,3); entity.Property(x=>x.LineTotal).HasPrecision(12,3); entity.Property(x=>x.Notes).HasMaxLength(500);
            entity.HasOne(x=>x.Order).WithMany(x=>x.Items).HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(x=>x.MenuItem).WithMany().HasForeignKey(x=>x.MenuItemId).OnDelete(DeleteBehavior.Restrict); entity.HasIndex(x=>x.OrderId);
            entity.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.Order.BranchId==RestaurantBranchId);
        });
        modelBuilder.Entity<OrderItemComboSelection>(entity =>
        {
            entity.HasKey(x=>x.Id); entity.Property(x=>x.PriceDeltaSnapshot).HasPrecision(12,3); entity.HasOne(x=>x.OrderItem).WithMany(x=>x.ComboSelections).HasForeignKey(x=>x.OrderItemId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(x=>x.ComboComponent).WithMany().HasForeignKey(x=>x.ComboComponentId).OnDelete(DeleteBehavior.Restrict); entity.HasOne(x=>x.SelectedMenuItem).WithMany().HasForeignKey(x=>x.SelectedMenuItemId).OnDelete(DeleteBehavior.Restrict); entity.HasIndex(x=>x.OrderItemId);
            entity.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.OrderItem.Order.BranchId==RestaurantBranchId);
        });
        modelBuilder.Entity<OrderItemModifier>(entity =>
        {
            entity.HasKey(x=>x.Id); entity.Property(x=>x.PriceDeltaSnapshot).HasPrecision(12,3); entity.HasOne(x=>x.OrderItem).WithMany(x=>x.Modifiers).HasForeignKey(x=>x.OrderItemId).OnDelete(DeleteBehavior.Cascade); entity.HasOne(x=>x.ModifierOption).WithMany().HasForeignKey(x=>x.ModifierOptionId).OnDelete(DeleteBehavior.Restrict); entity.HasIndex(x=>x.OrderItemId);
            entity.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.OrderItem.Order.BranchId==RestaurantBranchId);
        });
        modelBuilder.Entity<OrderCancellation>(entity =>
        {
            entity.HasKey(x=>x.Id); entity.Property(x=>x.Reason).IsRequired().HasMaxLength(500);
            entity.HasOne(x=>x.Order).WithMany(x=>x.Cancellations).HasForeignKey(x=>x.OrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x=>x.OrderItem).WithMany().HasForeignKey(x=>x.OrderItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x=>new{x.OrderId,x.CreatedAt}); entity.HasIndex(x=>x.OrderItemId); entity.HasIndex(x=>x.CancelledByUserId);
            entity.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.Order.BranchId==RestaurantBranchId);
        });
        modelBuilder.Entity<UnitOfMeasure>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Name).IsRequired().HasMaxLength(100);e.Property(x=>x.Symbol).IsRequired().HasMaxLength(20);e.HasIndex(x=>x.Name).IsUnique();});
        modelBuilder.Entity<Ingredient>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.NameAr).IsRequired().HasMaxLength(200);e.Property(x=>x.NameEn).IsRequired().HasMaxLength(200);e.HasOne(x=>x.UnitOfMeasure).WithMany().HasForeignKey(x=>x.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>x.UnitOfMeasureId);});
        modelBuilder.Entity<Warehouse>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.NameAr).IsRequired().HasMaxLength(200);e.Property(x=>x.NameEn).IsRequired().HasMaxLength(200);e.HasOne(x=>x.Branch).WithMany().HasForeignKey(x=>x.BranchId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.BranchId,x.NameEn}).IsUnique();e.HasIndex(x=>x.BranchId).IsUnique().HasFilter("\"IsDefault\" = TRUE");e.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.BranchId==RestaurantBranchId);});
        modelBuilder.Entity<WarehouseIngredientStock>(e=>{e.HasKey(x=>new{x.WarehouseId,x.IngredientId});e.Property(x=>x.CurrentQuantity).HasPrecision(18,3).IsConcurrencyToken();e.Property(x=>x.LowStockThreshold).HasPrecision(18,3);e.HasOne(x=>x.Warehouse).WithMany().HasForeignKey(x=>x.WarehouseId).OnDelete(DeleteBehavior.Cascade);e.HasOne(x=>x.Ingredient).WithMany().HasForeignKey(x=>x.IngredientId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>x.IngredientId);e.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.Warehouse.BranchId==RestaurantBranchId);});
        modelBuilder.Entity<MenuItemRecipeLine>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.QuantityRequired).HasPrecision(18,3);e.HasOne(x=>x.MenuItem).WithMany().HasForeignKey(x=>x.MenuItemId).OnDelete(DeleteBehavior.Cascade);e.HasOne(x=>x.Branch).WithMany().HasForeignKey(x=>x.BranchId).OnDelete(DeleteBehavior.Cascade);e.HasOne(x=>x.Ingredient).WithMany().HasForeignKey(x=>x.IngredientId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.MenuItemId,x.BranchId,x.IngredientId}).IsUnique();e.HasIndex(x=>x.BranchId);e.HasIndex(x=>x.IngredientId);e.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.BranchId==RestaurantBranchId);});
        modelBuilder.Entity<InventoryTransactionReason>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Code).IsRequired().HasMaxLength(60);e.Property(x=>x.NameAr).IsRequired().HasMaxLength(150);e.Property(x=>x.NameEn).IsRequired().HasMaxLength(150);e.HasIndex(x=>x.Code).IsUnique();e.HasData(Reason("PURCHASE_IN","شراء","Purchase"),Reason("TRANSFER_IN","تحويل وارد","Transfer in"),Reason("TRANSFER_OUT","تحويل صادر","Transfer out"),Reason("WASTE","هدر","Waste"),Reason("THEORETICAL_CONSUMPTION","استهلاك نظري","Theoretical consumption"),Reason("MANUAL_ADJUST","تعديل يدوي","Manual adjustment"),Reason("CANCELLATION_RETURN","مرتجع إلغاء","Cancellation return"),Reason("INVENTORY_COUNT_ADJUSTMENT","تسوية جرد","Inventory count adjustment"));});
        modelBuilder.Entity<RestaurantInventoryTransaction>(e=>{e.ToTable("InventoryTransactions");e.HasKey(x=>x.Id);e.Property(x=>x.QuantityChange).HasPrecision(18,3);e.HasOne(x=>x.Warehouse).WithMany().HasForeignKey(x=>x.WarehouseId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Ingredient).WithMany().HasForeignKey(x=>x.IngredientId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Reason).WithMany().HasForeignKey(x=>x.ReasonId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.ReferenceOrder).WithMany().HasForeignKey(x=>x.ReferenceOrderId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.WarehouseId,x.CreatedAt});e.HasIndex(x=>x.IngredientId);e.HasIndex(x=>x.ReasonId);e.HasIndex(x=>x.ReferenceOrderId);e.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.Warehouse.BranchId==RestaurantBranchId);});
        modelBuilder.Entity<StockCount>(e=>{e.HasKey(x=>x.Id);e.Property(x=>x.Status).IsRequired().HasMaxLength(20);e.ToTable(x=>x.HasCheckConstraint("CK_StockCounts_Status","\"Status\" IN ('Draft','Finalized')"));e.HasOne(x=>x.Branch).WithMany().HasForeignKey(x=>x.BranchId).OnDelete(DeleteBehavior.Restrict);e.HasOne(x=>x.Warehouse).WithMany().HasForeignKey(x=>x.WarehouseId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>new{x.BranchId,x.CreatedAt});e.HasIndex(x=>x.WarehouseId);e.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.BranchId==RestaurantBranchId);});
        modelBuilder.Entity<StockCountLine>(e=>{e.HasKey(x=>new{x.StockCountId,x.IngredientId});e.Property(x=>x.SystemQuantity).HasPrecision(18,3);e.Property(x=>x.CountedQuantity).HasPrecision(18,3);e.Property(x=>x.VarianceQuantity).HasPrecision(18,3);e.HasOne(x=>x.StockCount).WithMany(x=>x.Lines).HasForeignKey(x=>x.StockCountId).OnDelete(DeleteBehavior.Cascade);e.HasOne(x=>x.Ingredient).WithMany().HasForeignKey(x=>x.IngredientId).OnDelete(DeleteBehavior.Restrict);e.HasIndex(x=>x.IngredientId);e.HasQueryFilter(x=>BypassRestaurantBranchFilter||x.StockCount.BranchId==RestaurantBranchId);});

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(p => p.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(p => p.Category).IsRequired().HasMaxLength(100);
            entity.Property(p => p.Price).HasPrecision(18, 3);
        });

        modelBuilder.Entity<SalesChannel>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(c => c.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(c => c.LogoUrl).HasMaxLength(1000);
            entity.HasIndex(c => c.IsInStore).IsUnique().HasFilter("\"IsInStore\" = TRUE");
        });
        modelBuilder.Entity<ProductChannelPrice>(entity =>
        {
            entity.HasKey(p => new { p.ProductId, p.ChannelId });
            entity.Property(p => p.Price).HasPrecision(18, 3);
            entity.HasOne(p => p.Product).WithMany().HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(p => p.Channel).WithMany(c => c.ProductPrices).HasForeignKey(p => p.ChannelId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RawMaterial>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.NameAr).IsRequired().HasMaxLength(200);
            entity.Property(m => m.NameEn).IsRequired().HasMaxLength(200);
            entity.Property(m => m.Unit).IsRequired().HasMaxLength(50);
            entity.Property(m => m.MeasurementType).IsRequired().HasMaxLength(20);
        });

        modelBuilder.Entity<BranchRawMaterialStock>(entity =>
        {
            entity.Property(s => s.Version).IsRowVersion();
            entity.HasKey(s => new { s.BranchId, s.RawMaterialId });
            entity.HasOne(s => s.RawMaterial).WithMany().HasForeignKey(s => s.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(s => s.CurrentQuantity).HasPrecision(18, 3);
            entity.Property(s => s.LowStockThreshold).HasPrecision(18, 3);

            entity.HasQueryFilter(s =>
                _currentUser == null
                || _currentUser.BypassBranchFilter
                || s.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<ProductRecipe>(entity =>
        {
            entity.HasKey(r => new { r.ProductId, r.BranchId, r.RawMaterialId });
            entity.HasOne(r => r.Product).WithMany().HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(r => r.RawMaterial).WithMany().HasForeignKey(r => r.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(r => r.QuantityRequired).HasPrecision(18, 3);

            entity.HasQueryFilter(r =>
                _currentUser == null
                || _currentUser.BypassBranchFilter
                || r.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<StockAdjustment>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.HasOne(a => a.RawMaterial).WithMany().HasForeignKey(a => a.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(a => a.QuantityChange).HasPrecision(18, 3);
            entity.Property(a => a.Reason).IsRequired().HasMaxLength(500);
            entity.HasIndex(a => new { a.BranchId, a.RawMaterialId });

            entity.HasQueryFilter(a =>
                _currentUser == null
                || _currentUser.BypassBranchFilter
                || a.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<SupplyPackage>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NameAr).IsRequired().HasMaxLength(150);
            entity.Property(x => x.NameEn).IsRequired().HasMaxLength(150);
            entity.Property(x => x.BaseQuantity).HasPrecision(18, 3);
            entity.HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.RawMaterialId, x.NameEn }).IsUnique();
        });

        modelBuilder.Entity<StockReceipt>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PackageCount).HasPrecision(18, 3);
            entity.Property(x => x.BaseQuantityAdded).HasPrecision(18, 3);
            entity.Property(x => x.PackageNameSnapshot).IsRequired().HasMaxLength(150);
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne(x => x.RawMaterial).WithMany().HasForeignKey(x => x.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.SupplyPackage).WithMany().HasForeignKey(x => x.SupplyPackageId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.BranchId, x.ReceivedAt });
            entity.HasQueryFilter(x => _currentUser == null || _currentUser.BypassBranchFilter || x.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Revision).IsConcurrencyToken();
            entity.Property(s => s.CashAmount).HasPrecision(18, 3);
            entity.Property(s => s.CardAmount).HasPrecision(18, 3);
            entity.Property(s => s.TotalAmount).HasPrecision(18, 3);
            entity.Property(s => s.DiscountValue).HasPrecision(18, 3);
            entity.Property(s => s.DiscountAmount).HasPrecision(18, 3);
            entity.Property(s => s.DiscountType).IsRequired().HasMaxLength(20);
            entity.Property(s => s.PaymentMethod).IsRequired().HasMaxLength(20);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(20);
            entity.HasIndex(s => new { s.BranchId, s.BusinessDate });
            entity.HasIndex(s => new { s.BranchId, s.SaleNumber }).IsUnique();
            entity.HasOne(s => s.Shift).WithMany(s => s.Sales).HasForeignKey(s => s.ShiftId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.Channel).WithMany().HasForeignKey(s => s.ChannelId).OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(s =>
                _currentUser == null
                || _currentUser.BypassBranchFilter
                || s.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<SaleEdit>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Sale).WithMany().HasForeignKey(e => e.SaleId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.EditedByName).HasMaxLength(200);
            entity.HasIndex(e => new { e.SaleId, e.CreatedAt });
            entity.HasQueryFilter(e => _currentUser == null || _currentUser.BypassBranchFilter || e.Sale.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasOne(i => i.Sale).WithMany(s => s.Items).HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(i => i.Product).WithMany().HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Restrict);
            entity.Property(i => i.ProductNameSnapshot).IsRequired().HasMaxLength(200);
            entity.Property(i => i.UnitPriceSnapshot).HasPrecision(18, 3);
            entity.Property(i => i.Quantity).HasPrecision(18, 3);
            entity.Property(i => i.LineTotal).HasPrecision(18, 3);
            entity.Property(i => i.DiscountValue).HasPrecision(18, 3);
            entity.Property(i => i.DiscountType).IsRequired().HasMaxLength(20);

            // Mirrors Sale's branch filter so Include(i => i.Sale) can't leak cross-branch items.
            entity.HasQueryFilter(i =>
                _currentUser == null
                || _currentUser.BypassBranchFilter
                || i.Sale.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<SaleInventoryConsumption>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasOne(c => c.Sale).WithMany(s => s.InventoryConsumptions).HasForeignKey(c => c.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(c => c.RawMaterial).WithMany().HasForeignKey(c => c.RawMaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(c => c.QuantityConsumed).HasPrecision(18, 3);
            entity.HasIndex(c => c.SaleId);
            entity.HasQueryFilter(c =>
                _currentUser == null || _currentUser.BypassBranchFilter || c.Sale.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<Shift>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.OpeningCash).HasPrecision(18, 3);
            entity.Property(s => s.ClosingCashExpected).HasPrecision(18, 3);
            entity.Property(s => s.ClosingCashActual).HasPrecision(18, 3);
            entity.Property(s => s.VarianceAmount).HasPrecision(18, 3);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(20);
            entity.HasIndex(s => new { s.BranchId, s.OpenedAt });
            entity.HasIndex(s => new { s.CashierUserId, s.Status });
            entity.HasIndex(s => s.CashierUserId).IsUnique().HasFilter("\"Status\" = 'Open'");
            entity.Property(s => s.Version).IsRowVersion();

            entity.HasQueryFilter(s =>
                _currentUser == null || _currentUser.BypassBranchFilter || s.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<ShiftCashCount>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasOne(c => c.Shift).WithMany(s => s.CashCounts).HasForeignKey(c => c.ShiftId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(c => c.CountType).IsRequired().HasMaxLength(20);
            entity.Property(c => c.Denomination).HasPrecision(18, 3);
            entity.HasIndex(c => new { c.ShiftId, c.CountType, c.Denomination }).IsUnique();
            entity.HasQueryFilter(c =>
                _currentUser == null || _currentUser.BypassBranchFilter || c.Shift.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<VoidRequest>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.Property(v => v.Reason).IsRequired().HasMaxLength(500);
            entity.HasIndex(v => v.SaleId).IsUnique();
            entity.HasOne(v => v.Sale).WithOne(s => s.VoidRequest).HasForeignKey<VoidRequest>(v => v.SaleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(v =>
                _currentUser == null || _currentUser.BypassBranchFilter || v.Sale.BranchId == _currentUser.BranchId);
        });

        modelBuilder.Entity<ClosingScheduleConfig>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasData(new ClosingScheduleConfig
            {
                Id = Guid.Parse("b2b60295-51db-4ad0-aa5f-93a1c196a97f"),
                DefaultCloseTime = new TimeOnly(23, 45),
                IsActive = true,
            });
        });

        modelBuilder.Entity<ClosingScheduleException>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => new { e.Date, e.BranchId }).IsUnique().HasFilter("\"BranchId\" IS NOT NULL");
            entity.HasIndex(e => e.Date).IsUnique().HasFilter("\"BranchId\" IS NULL");
            entity.HasQueryFilter(e =>
                _currentUser == null || _currentUser.BypassBranchFilter || e.BranchId == null || e.BranchId == _currentUser.BranchId);
        });
        modelBuilder.Entity<LowStockNotification>(entity =>
        {
            entity.HasKey(n => n.Id);
            entity.HasOne(n => n.Branch).WithMany().HasForeignKey(n => n.BranchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(n => n.RawMaterial).WithMany().HasForeignKey(n => n.RawMaterialId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(n => new { n.BranchId, n.RawMaterialId }).IsUnique().HasFilter("\"ResolvedAt\" IS NULL");
            entity.HasQueryFilter(n => _currentUser == null || _currentUser.BypassBranchFilter || n.BranchId == _currentUser.BranchId);
        });
        modelBuilder.Entity<AiProviderSetting>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Provider).HasMaxLength(50); entity.Property(x => x.Model).HasMaxLength(100); entity.Property(x => x.BaseUrl).HasMaxLength(500); });
        modelBuilder.Entity<AiInsightRequest>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.RequestType).HasMaxLength(50); entity.Property(x => x.ResultSummary).HasMaxLength(8000); entity.HasIndex(x => x.CreatedAt); });
        modelBuilder.Entity<EmailSettings>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SmtpHost).HasMaxLength(255);
            entity.Property(x => x.Username).HasMaxLength(255);
            entity.Property(x => x.FromEmail).HasMaxLength(320);
            entity.Property(x => x.FromName).HasMaxLength(200);
            entity.Property(x => x.Recipients).HasMaxLength(2000);
        });
        modelBuilder.Entity<ReceiptSettings>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.HeaderText).HasMaxLength(500);
        });
    }
    private static InventoryTransactionReason Reason(string code,string ar,string en)=>new(){Id=new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(code))),Code=code,NameAr=ar,NameEn=en,IsActive=true};
}
