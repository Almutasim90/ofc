using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Ofc;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/tables")]
[Authorize]
public class TablesController(TableService tableService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TableDto>>> GetAll([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        return Ok(await tableService.GetAllAsync(branchId, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.TablesManage)]
    public async Task<ActionResult<TableDto>> Create(CreateTableRequest request, CancellationToken cancellationToken)
    {
        var table = await tableService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = table.Id }, table);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.TablesManage)]
    public async Task<ActionResult<TableDto>> Update(Guid id, UpdateTableRequest request, CancellationToken cancellationToken)
    {
        return Ok(await tableService.UpdateAsync(id, request, cancellationToken));
    }
}
