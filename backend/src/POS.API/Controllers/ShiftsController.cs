using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Shifts;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/shifts")]
[RequirePermission(PermissionKeys.SalesCreate)]
public class ShiftsController(ShiftService shiftService) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<ShiftDto?>> GetCurrent(CancellationToken cancellationToken)
    {
        var shift = await shiftService.GetCurrentAsync(cancellationToken);
        return shift is null ? NoContent() : Ok(shift);
    }

    [HttpPost("open")]
    public async Task<ActionResult<ShiftDto>> Open(OpenShiftRequest request, CancellationToken cancellationToken)
    {
        var shift = await shiftService.OpenAsync(request, cancellationToken);
        return Created($"api/shifts/{shift.Id}", shift);
    }

    [HttpGet("latest-closed")]
    public async Task<ActionResult<ShiftDto?>> GetLatestClosed(CancellationToken cancellationToken)
    {
        var shift = await shiftService.GetLatestClosedAsync(cancellationToken);
        return shift is null ? NoContent() : Ok(shift);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<ShiftDto>> Close(Guid id, CloseShiftRequest request, CancellationToken cancellationToken)
        => Ok(await shiftService.CloseAsync(id, request, cancellationToken));
}
