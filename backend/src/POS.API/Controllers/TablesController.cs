using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.TableManagement;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController, Route("api/tables"), Authorize, RequirePermission(PermissionKeys.TablesManage)]
public class TablesController(TableManagementService service) : ControllerBase
{
    [HttpGet("floors")]
    public async Task<ActionResult<List<RestaurantFloorDto>>> Floors([FromQuery] Guid branchId, CancellationToken ct) => Ok(await service.GetFloorsAsync(branchId, ct));

    [HttpPost("floors")]
    public async Task<ActionResult<RestaurantFloorDto>> CreateFloor(SaveRestaurantFloorRequest request, CancellationToken ct) => Ok(await service.SaveFloorAsync(null, request, ct));

    [HttpPut("floors/{id:guid}")]
    public async Task<ActionResult<RestaurantFloorDto>> UpdateFloor(Guid id, SaveRestaurantFloorRequest request, CancellationToken ct) => Ok(await service.SaveFloorAsync(id, request, ct));

    [HttpDelete("floors/{id:guid}")]
    public async Task<IActionResult> DeleteFloor(Guid id, CancellationToken ct) { await service.DeleteFloorAsync(id, ct); return NoContent(); }

    [HttpGet]
    public async Task<ActionResult<List<TableLayoutDto>>> Tables([FromQuery] Guid branchId, [FromQuery] Guid? floorId, CancellationToken ct) => Ok(await service.GetTablesAsync(branchId, floorId, ct));

    [HttpPost]
    public async Task<ActionResult<TableLayoutDto>> CreateTable(SaveTableLayoutRequest request, CancellationToken ct) => Ok(await service.SaveTableAsync(null, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TableLayoutDto>> UpdateTable(Guid id, SaveTableLayoutRequest request, CancellationToken ct) => Ok(await service.SaveTableAsync(id, request, ct));

    [HttpGet("board")]
    public async Task<ActionResult<List<TableStatusDto>>> Board([FromQuery] Guid branchId, [FromQuery] Guid? floorId, CancellationToken ct) => Ok(await service.GetBoardAsync(branchId, floorId, ct));
}
