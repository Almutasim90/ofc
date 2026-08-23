using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Closing;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/closing-schedule")]
public class ClosingScheduleController(ClosingScheduleService service) : ControllerBase
{
    [HttpGet("config")]
    [RequirePermission(PermissionKeys.ClosingConfigure)]
    public async Task<ActionResult<ClosingScheduleConfigDto>> GetConfig(CancellationToken cancellationToken) =>
        Ok(await service.GetConfigAsync(cancellationToken));

    [HttpPut("config")]
    [RequirePermission(PermissionKeys.ClosingConfigure)]
    public async Task<ActionResult<ClosingScheduleConfigDto>> UpdateConfig(
        UpdateClosingScheduleConfigRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateConfigAsync(request, cancellationToken));

    [HttpGet("exceptions")]
    [RequirePermission(PermissionKeys.ClosingConfigure)]
    public async Task<ActionResult<List<ClosingScheduleExceptionDto>>> GetExceptions(CancellationToken cancellationToken) =>
        Ok(await service.GetExceptionsAsync(cancellationToken));

    [HttpPost("exceptions")]
    [RequirePermission(PermissionKeys.ClosingConfigure)]
    public async Task<ActionResult<ClosingScheduleExceptionDto>> CreateException(
        UpsertClosingScheduleExceptionRequest request, CancellationToken cancellationToken)
    {
        var result = await service.CreateExceptionAsync(request, cancellationToken);
        return Created($"api/closing-schedule/exceptions/{result.Id}", result);
    }

    [HttpPut("exceptions/{id:guid}")]
    [RequirePermission(PermissionKeys.ClosingConfigure)]
    public async Task<ActionResult<ClosingScheduleExceptionDto>> UpdateException(
        Guid id, UpsertClosingScheduleExceptionRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateExceptionAsync(id, request, cancellationToken));

    [HttpDelete("exceptions/{id:guid}")]
    [RequirePermission(PermissionKeys.ClosingConfigure)]
    public async Task<IActionResult> DeleteException(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteExceptionAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("upcoming")]
    [RequirePermission(PermissionKeys.SalesCreate)]
    public async Task<ActionResult<UpcomingClosingDto>> GetUpcoming(CancellationToken cancellationToken) =>
        Ok(await service.GetUpcomingAsync(cancellationToken));
}
