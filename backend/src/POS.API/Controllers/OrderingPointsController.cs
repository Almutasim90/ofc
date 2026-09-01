using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using POS.API.Authorization;using POS.Application.QrOrdering;using POS.Domain.Constants;
namespace POS.API.Controllers;
[ApiController,Route("api/ordering-points"),Authorize,RequirePermission(PermissionKeys.TablesManage)]
public class OrderingPointsController(QrOrderingService service):ControllerBase
{
 [HttpGet]public async Task<ActionResult<List<OrderingPointDto>>>Points([FromQuery]Guid branchId,CancellationToken ct)=>Ok(await service.GetPointsAsync(branchId,ct));
 [HttpPost]public async Task<ActionResult<OrderingPointDto>>Create(SaveOrderingPointRequest request,CancellationToken ct)=>Ok(await service.SavePointAsync(null,request,ct));
 [HttpPut("{id:guid}")]public async Task<ActionResult<OrderingPointDto>>Update(Guid id,SaveOrderingPointRequest request,CancellationToken ct)=>Ok(await service.SavePointAsync(id,request,ct));
 [HttpPost("{id:guid}/regenerate")]public async Task<ActionResult<object>>Regenerate(Guid id,CancellationToken ct)=>Ok(new{qrToken=await service.RegenerateAsync(id,ct)});
 [HttpGet("bays")]public async Task<ActionResult<List<CarPickupBayDto>>>Bays([FromQuery]Guid branchId,CancellationToken ct)=>Ok(await service.GetBaysAsync(branchId,ct));
 [HttpPost("bays")]public async Task<ActionResult<CarPickupBayDto>>CreateBay(SaveCarPickupBayRequest request,CancellationToken ct)=>Ok(await service.SaveBayAsync(null,request,ct));
 [HttpPut("bays/{id:guid}")]public async Task<ActionResult<CarPickupBayDto>>UpdateBay(Guid id,SaveCarPickupBayRequest request,CancellationToken ct)=>Ok(await service.SaveBayAsync(id,request,ct));
 [HttpPost("sessions/{id:guid}/close")]public async Task<IActionResult>Close(Guid id,CancellationToken ct){await service.CloseAsync(id,ct);return NoContent();}
}
