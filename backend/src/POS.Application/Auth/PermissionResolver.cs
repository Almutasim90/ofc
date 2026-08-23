using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;

namespace POS.Application.Auth;

public static class PermissionResolver
{
    /// <summary>
    /// Effective permissions = role permissions, then apply per-user overrides
    /// (Grant adds, Deny removes).
    /// </summary>
    public static async Task<IReadOnlyCollection<string>> ResolveAsync(
        IAppDbContext db, Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var rolePermissionKeys = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.Permission.Key)
            .ToListAsync(cancellationToken);

        var overrides = await db.UserPermissionOverrides
            .Where(o => o.UserId == userId)
            .Select(o => new { o.Permission.Key, o.IsGranted })
            .ToListAsync(cancellationToken);

        var effective = new HashSet<string>(rolePermissionKeys);
        foreach (var o in overrides)
        {
            if (o.IsGranted)
            {
                effective.Add(o.Key);
            }
            else
            {
                effective.Remove(o.Key);
            }
        }

        return effective;
    }
}
