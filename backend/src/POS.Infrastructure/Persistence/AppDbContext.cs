using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    private readonly ICurrentUserService? _currentUser;

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
    public DbSet<Product> Products => Set<Product>();
    public DbSet<SalesChannel> SalesChannels => Set<SalesChannel>();
    public DbSet<ProductChannelPrice> ProductChannelPrices => Set<ProductChannelPrice>();
    public DbSet<RawMaterial> RawMaterials => Set<RawMaterial>();
    public DbSet<BranchRawMaterialStock> BranchRawMaterialStocks => Set<BranchRawMaterialStock>();
    public DbSet<ProductRecipe> ProductRecipes => Set<ProductRecipe>();
    public DbSet<StockAdjustment> StockAdjustments => Set<StockAdjustment>();
    public DbSet<SupplyPackage> SupplyPackages => Set<SupplyPackage>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();

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
            entity.Property(s => s.TotalAmount).HasPrecision(18, 3);
            entity.Property(s => s.DiscountValue).HasPrecision(18, 3);
            entity.Property(s => s.DiscountAmount).HasPrecision(18, 3);
            entity.Property(s => s.DiscountType).IsRequired().HasMaxLength(20);
            entity.Property(s => s.PaymentMethod).IsRequired().HasMaxLength(20);
            entity.Property(s => s.Status).IsRequired().HasMaxLength(20);
            entity.HasIndex(s => new { s.BranchId, s.BusinessDate });
            entity.HasOne(s => s.Shift).WithMany(s => s.Sales).HasForeignKey(s => s.ShiftId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.Channel).WithMany().HasForeignKey(s => s.ChannelId).OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(s =>
                _currentUser == null
                || _currentUser.BypassBranchFilter
                || s.BranchId == _currentUser.BranchId);
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
        modelBuilder.Entity<AiProviderSetting>(entity => { entity.HasKey(x => x.Id); entity.Property(x => x.Provider).HasMaxLength(50); entity.Property(x => x.Model).HasMaxLength(100); });
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
    }
}
