using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Modifiers;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController, Route("api/modifiers"), Authorize]
public class ModifiersController(ModifierService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<List<ModifierGroupDto>>> Get([FromQuery] Guid? menuItemId, CancellationToken ct) => Ok(await service.GetAsync(menuItemId, ct));
    [HttpPost, RequirePermission(PermissionKeys.ModifiersManage)] public async Task<ActionResult<ModifierGroupDto>> Create(SaveModifierGroupRequest request, CancellationToken ct) => Ok(await service.SaveAsync(null, request, ct));
    [HttpPut("{id:guid}"), RequirePermission(PermissionKeys.ModifiersManage)] public async Task<ActionResult<ModifierGroupDto>> Update(Guid id, SaveModifierGroupRequest request, CancellationToken ct) => Ok(await service.SaveAsync(id, request, ct));
    [HttpDelete("{id:guid}"), RequirePermission(PermissionKeys.ModifiersManage)] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
    [HttpPost("validate-selection")] public async Task<ActionResult<ValidatedModifierSelectionDto>> ValidateSelection(ValidateModifierSelectionRequest request, CancellationToken ct) => Ok(await service.ValidateSelectionAsync(request, ct));
}
