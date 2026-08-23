using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Users;

public class UserService(IAppDbContext db, IPasswordHasher passwordHasher, ICurrentUserService currentUser)
{
    public async Task<List<UserDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Users
            .Include(u => u.Role)
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(
                u.Id, u.FullName, u.Username, u.BranchId, u.RoleId, u.Role.Name,
                u.PreferredLanguage, u.PreferredTheme, u.IsActive, u.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(request.BranchId);

        var usernameTaken = await db.Users.AnyAsync(u => u.Username == request.Username, cancellationToken);
        if (usernameTaken)
        {
            throw new ValidationException($"Username '{request.Username}' is already taken.");
        }

        var role = await db.Roles.FindAsync([request.RoleId], cancellationToken)
            ?? throw new NotFoundException($"Role '{request.RoleId}' not found.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Username = request.Username,
            PasswordHash = passwordHasher.Hash(request.Password),
            BranchId = request.BranchId,
            RoleId = role.Id,
            PreferredLanguage = request.PreferredLanguage,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        return new UserDto(user.Id, user.FullName, user.Username, user.BranchId, user.RoleId, role.Name,
            user.PreferredLanguage, user.PreferredTheme, user.IsActive, user.CreatedAt);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        EnsureBranchScope(request.BranchId);

        var user = await db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException($"User '{id}' not found.");

        var role = await db.Roles.FindAsync([request.RoleId], cancellationToken)
            ?? throw new NotFoundException($"Role '{request.RoleId}' not found.");

        user.FullName = request.FullName;
        user.BranchId = request.BranchId;
        user.RoleId = role.Id;
        user.Role = role;
        user.PreferredLanguage = request.PreferredLanguage;
        user.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);

        return new UserDto(user.Id, user.FullName, user.Username, user.BranchId, user.RoleId, role.Name,
            user.PreferredLanguage, user.PreferredTheme, user.IsActive, user.CreatedAt);
    }

    public async Task<UserDto> UpdateMyPreferencesAsync(Guid userId, UpdateMyPreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.Include(u => u.Role)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException($"User '{userId}' not found.");

        user.PreferredLanguage = request.PreferredLanguage;
        user.PreferredTheme = request.PreferredTheme;

        await db.SaveChangesAsync(cancellationToken);

        return new UserDto(user.Id, user.FullName, user.Username, user.BranchId, user.RoleId, user.Role.Name,
            user.PreferredLanguage, user.PreferredTheme, user.IsActive, user.CreatedAt);
    }

    public async Task<List<PermissionOverrideDto>> GetPermissionOverridesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundException($"User '{userId}' not found.");
        }

        var overrides = await db.UserPermissionOverrides
            .Where(o => o.UserId == userId)
            .ToDictionaryAsync(o => o.PermissionId, o => o.IsGranted, cancellationToken);

        var permissions = await db.Permissions.OrderBy(p => p.Key).ToListAsync(cancellationToken);

        return permissions
            .Select(p => new PermissionOverrideDto(
                p.Id,
                p.Key,
                overrides.TryGetValue(p.Id, out var isGranted) ? isGranted : (bool?)null))
            .ToList();
    }

    public async Task SetPermissionOverrideAsync(Guid userId, SetPermissionOverrideRequest request, CancellationToken cancellationToken = default)
    {
        var userExists = await db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundException($"User '{userId}' not found.");
        }

        var existing = await db.UserPermissionOverrides
            .FirstOrDefaultAsync(o => o.UserId == userId && o.PermissionId == request.PermissionId, cancellationToken);

        if (request.IsGranted is null)
        {
            if (existing is not null)
            {
                db.UserPermissionOverrides.Remove(existing);
            }
        }
        else if (existing is not null)
        {
            existing.IsGranted = request.IsGranted.Value;
        }
        else
        {
            db.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                UserId = userId,
                PermissionId = request.PermissionId,
                IsGranted = request.IsGranted.Value,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private void EnsureBranchScope(Guid? branchId)
    {
        if (!currentUser.BypassBranchFilter && branchId != currentUser.BranchId)
        {
            throw new ValidationException("You do not have access to manage users outside your branch.");
        }
    }
}
