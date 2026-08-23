using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using POS.Application.Abstractions;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Services;

public class JwtTokenService(JwtOptions options) : IJwtTokenService
{
    public string GenerateToken(User user, IReadOnlyCollection<string> permissions)
    {
        var claims = new List<Claim>
        {
            new(AppClaimTypes.UserId, user.Id.ToString()),
            new(AppClaimTypes.Role, user.Role.Name),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (user.BranchId is not null)
        {
            claims.Add(new Claim(AppClaimTypes.BranchId, user.BranchId.Value.ToString()));
        }

        claims.AddRange(permissions.Select(p => new Claim(AppClaimTypes.Permission, p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
