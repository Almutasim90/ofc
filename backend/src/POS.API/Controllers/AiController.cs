using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.AI;
using POS.Domain.Constants;
namespace POS.API.Controllers;
[ApiController, Route("api/ai")]
public class AiController(AiInsightService service) : ControllerBase
{
    [HttpGet("settings"), RequirePermission(PermissionKeys.AiManage)] public async Task<ActionResult<AiSettingsDto>> Settings(CancellationToken ct) => Ok(await service.GetSettingsAsync(ct));
    [HttpPut("settings"), RequirePermission(PermissionKeys.AiManage)] public async Task<ActionResult<AiSettingsDto>> Settings(UpdateAiSettingsRequest r, CancellationToken ct) => Ok(await service.SaveSettingsAsync(r, ct));
    [HttpPost("insights"), RequirePermission(PermissionKeys.ReportsBranchView)] public async Task<ActionResult<AiInsightDto>> Insight(GenerateInsightRequest r, CancellationToken ct) => Ok(await service.GenerateAsync(r, ct));
}
