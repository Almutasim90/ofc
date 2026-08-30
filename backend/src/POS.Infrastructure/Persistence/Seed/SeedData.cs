using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Seed;

public static class SeedData
{
    public const string BootstrapAdminUsername = "admin";
    public const string BootstrapAdminPassword = "Admin@12345";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher passwordHasher, CancellationToken cancellationToken = default)
    {
        var existingPermissionKeys = await db.Permissions.Select(p => p.Key).ToListAsync(cancellationToken);
        var missingPermissionKeys = PermissionKeys.All.Except(existingPermissionKeys).ToList();
        if (missingPermissionKeys.Count > 0)
        {
            db.Permissions.AddRange(missingPermissionKeys.Select(key => new Permission
            {
                Id = Guid.NewGuid(),
                Key = key,
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.SalesChannels.AnyAsync(c => c.IsInStore, cancellationToken))
        {
            db.SalesChannels.Add(new SalesChannel { Id = SalesChannelIds.InStore, NameAr = "المحل", NameEn = "In-store", IsActive = true, IsInStore = true });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.Roles.AnyAsync(cancellationToken))
        {
            db.Roles.AddRange(RoleNames.All.Select(name => new Role
            {
                Id = Guid.NewGuid(),
                Name = name,
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        var roles = await db.Roles.ToListAsync(cancellationToken);
        var permissions = await db.Permissions.ToListAsync(cancellationToken);
        var existingRolePermissions = await db.RolePermissions.Select(rp => new { rp.RoleId, rp.PermissionId }).ToListAsync(cancellationToken);
        foreach (var role in roles)
        foreach (var key in DefaultRolePermissions.ByRole[role.Name])
        {
            var permission = permissions.First(p => p.Key == key);
            if (!existingRolePermissions.Any(x => x.RoleId == role.Id && x.PermissionId == permission.Id))
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }
        await db.SaveChangesAsync(cancellationToken);

        if (!await db.Users.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            var generalManagerRole = await db.Roles
                .FirstAsync(r => r.Name == RoleNames.GeneralManager, cancellationToken);

            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                FullName = "System Administrator",
                Username = BootstrapAdminUsername,
                PasswordHash = passwordHasher.Hash(BootstrapAdminPassword),
                BranchId = null,
                RoleId = generalManagerRole.Id,
                PreferredLanguage = "ar",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
