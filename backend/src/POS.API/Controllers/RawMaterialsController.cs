using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Catalog;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/raw-materials")]
[Authorize]
public class RawMaterialsController(RawMaterialService rawMaterialService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RawMaterialDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await rawMaterialService.GetAllAsync(cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<ActionResult<RawMaterialDto>> Create(CreateRawMaterialRequest request, CancellationToken cancellationToken)
    {
        var material = await rawMaterialService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = material.Id }, material);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<ActionResult<RawMaterialDto>> Update(Guid id, UpdateRawMaterialRequest request, CancellationToken cancellationToken)
    {
        return Ok(await rawMaterialService.UpdateAsync(id, request, cancellationToken));
    }
}
