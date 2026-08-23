using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.API.Authorization;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/roles")]
[RequirePermission(PermissionKeys.UsersManage)]
public class RolesController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RoleDto>>> GetAll(CancellationToken cancellationToken)
    {
        var roles = await db.Roles
            .OrderBy(r => r.Name)
            .Select(r => new RoleDto(r.Id, r.Name, r.Description))
            .ToListAsync(cancellationToken);

        return Ok(roles);
    }
}
