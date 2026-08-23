using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;

namespace POS.Application.Auth;

public class AuthService(IAppDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
{
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await db.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

        if (user is null || !user.IsActive || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw new UnauthorizedException("Invalid username or password.");
        }

        var permissions = await PermissionResolver.ResolveAsync(db, user.Id, user.RoleId, cancellationToken);
        var token = jwtTokenService.GenerateToken(user, permissions);

        return new LoginResponse(
            token,
            user.Id,
            user.FullName,
            user.BranchId,
            user.Role.Name,
            user.PreferredLanguage,
            user.PreferredTheme,
            permissions);
    }
}
