using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Ofc;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/menu-items")]
[Authorize]
public class MenuItemsController(MenuItemService menuItemService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MenuItemDto>>> GetAll([FromQuery] Guid? categoryId, CancellationToken cancellationToken)
    {
        return Ok(await menuItemService.GetAllAsync(categoryId, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<ActionResult<MenuItemDto>> Create(CreateMenuItemRequest request, CancellationToken cancellationToken)
    {
        var item = await menuItemService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = item.Id }, item);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<ActionResult<MenuItemDto>> Update(Guid id, UpdateMenuItemRequest request, CancellationToken cancellationToken)
    {
        return Ok(await menuItemService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/combo-components")]
    public async Task<ActionResult<List<ComboComponentDto>>> GetComboComponents(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await menuItemService.GetComboComponentsAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}/combo-components")]
    [RequirePermission(PermissionKeys.CombosManage)]
    public async Task<IActionResult> SetComboComponents(Guid id, SetComboComponentsRequest request, CancellationToken cancellationToken)
    {
        await menuItemService.SetComboComponentsAsync(id, request, cancellationToken);
        return NoContent();
    }
}
