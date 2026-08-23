using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.API.Authorization;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/permissions")]
[RequirePermission(PermissionKeys.UsersManage)]
public class PermissionsController(IAppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PermissionDto>>> GetAll(CancellationToken cancellationToken)
    {
        var permissions = await db.Permissions
            .OrderBy(p => p.Key)
            .Select(p => new PermissionDto(p.Id, p.Key, p.Description))
            .ToListAsync(cancellationToken);

        return Ok(permissions);
    }
}
