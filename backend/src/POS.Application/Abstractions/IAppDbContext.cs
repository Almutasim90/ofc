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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
