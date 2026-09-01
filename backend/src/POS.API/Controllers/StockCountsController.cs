using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.RestaurantInventory;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/stock-counts")]
[Authorize]
[RequirePermission(PermissionKeys.InventoryAdjust)]
public class StockCountsController(StockCountService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<StockCountDto>> Start(StartInput request, CancellationToken ct)
    {
        var count = await service.Start(request.BranchId, request.WarehouseId, ct);
        return CreatedAtAction(nameof(Get), new { id = count.Id }, count);
    }

    [HttpGet("draft")]
    public async Task<ActionResult<StockCountDto?>> GetDraft(Guid warehouseId, CancellationToken ct)
    {
        return Ok(await service.GetDraft(warehouseId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockCountDto>> Get(Guid id, CancellationToken ct)
    {
        return Ok(await service.Get(id, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Save(Guid id, List<CountLineInput>? request, CancellationToken ct)
    {
        await service.Save(id, request, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid id, CancellationToken ct)
    {
        await service.Finalize(id, ct);
        return NoContent();
    }

    public record StartInput(Guid BranchId, Guid WarehouseId);
}
