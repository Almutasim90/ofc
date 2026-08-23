using Microsoft.AspNetCore.Authorization;

namespace POS.API.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
