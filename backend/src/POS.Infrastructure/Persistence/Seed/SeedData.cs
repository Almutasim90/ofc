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
        if (!await db.Permissions.AnyAsync(cancellationToken))
        {
            db.Permissions.AddRange(PermissionKeys.All.Select(key => new Permission
            {
                Id = Guid.NewGuid(),
                Key = key,
            }));
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

        if (!await db.RolePermissions.AnyAsync(cancellationToken))
        {
            var roles = await db.Roles.ToListAsync(cancellationToken);
            var permissions = await db.Permissions.ToListAsync(cancellationToken);

            foreach (var role in roles)
            {
                var defaultKeys = DefaultRolePermissions.ByRole[role.Name];
                foreach (var key in defaultKeys)
                {
                    var permission = permissions.First(p => p.Key == key);
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }

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
