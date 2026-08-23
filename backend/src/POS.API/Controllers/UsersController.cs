using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Users;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/users")]
[RequirePermission(PermissionKeys.UsersManage)]
public class UsersController(UserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await userService.GetAllAsync(cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDto>> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        return Ok(await userService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/permission-overrides")]
    public async Task<ActionResult<List<PermissionOverrideDto>>> GetPermissionOverrides(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await userService.GetPermissionOverridesAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}/permission-overrides")]
    public async Task<IActionResult> SetPermissionOverride(Guid id, SetPermissionOverrideRequest request, CancellationToken cancellationToken)
    {
        await userService.SetPermissionOverrideAsync(id, request, cancellationToken);
        return NoContent();
    }
}
