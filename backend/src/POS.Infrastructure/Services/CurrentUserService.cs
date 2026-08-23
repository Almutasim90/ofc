using Microsoft.AspNetCore.Http;
using POS.Application.Abstractions;
using POS.Domain.Constants;

namespace POS.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private System.Security.Claims.ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var value = User?.FindFirst(AppClaimTypes.UserId)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public Guid? BranchId
    {
        get
        {
            var value = User?.FindFirst(AppClaimTypes.BranchId)?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? RoleName => User?.FindFirst(AppClaimTypes.Role)?.Value;

    public IReadOnlyCollection<string> Permissions =>
        User?.FindAll(AppClaimTypes.Permission).Select(c => c.Value).ToList() ?? [];

    public bool BypassBranchFilter =>
        RoleName == RoleNames.GeneralManager || Permissions.Contains(PermissionKeys.ReportsGlobalView);
}
