using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Users;

namespace POS.API.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(UserService userService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPut("preferences")]
    public async Task<ActionResult<UserDto>> UpdatePreferences(UpdateMyPreferencesRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");
        return Ok(await userService.UpdateMyPreferencesAsync(userId, request, cancellationToken));
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword(ChangeMyPasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");
        await userService.ChangeMyPasswordAsync(userId, request, cancellationToken);
        return NoContent();
    }
}
