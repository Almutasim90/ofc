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
    [HttpPost("settings/test"), RequirePermission(PermissionKeys.AiManage)] public async Task<ActionResult<AiTestResultDto>> TestConnection(CancellationToken ct) => Ok(await service.TestConnectionAsync(ct));
    [HttpPost("insights"), RequirePermission(PermissionKeys.ReportsBranchView)] public async Task<ActionResult<AiInsightDto>> Insight(GenerateInsightRequest r, CancellationToken ct) => Ok(await service.GenerateAsync(r, ct));
    [HttpGet("insights"), RequirePermission(PermissionKeys.ReportsBranchView)] public async Task<ActionResult<IReadOnlyList<AiInsightDto>>> RecentInsights([FromQuery] int take, CancellationToken ct) => Ok(await service.ListRecentAsync(take <= 0 ? 10 : take, ct));
}
