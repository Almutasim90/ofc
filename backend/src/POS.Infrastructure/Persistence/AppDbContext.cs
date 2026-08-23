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
            entity.HasOne(u => u.Role).WithMany().HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
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
    }
}
