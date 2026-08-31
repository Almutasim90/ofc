using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Channels;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController, Route("api/channels")]
public class ChannelsController(ChannelService service) : ControllerBase
{
    [HttpGet, RequirePermission(PermissionKeys.SalesCreate)]
    public async Task<ActionResult<List<SalesChannelDto>>> GetAll([FromQuery] bool activeOnly, CancellationToken ct) => Ok(await service.GetAllAsync(activeOnly, ct));
    [HttpPost, RequirePermission(PermissionKeys.ChannelsManage)]
    public async Task<ActionResult<SalesChannelDto>> Create(UpsertSalesChannelRequest request, CancellationToken ct) => Ok(await service.CreateAsync(request, ct));
    [HttpPut("{id:guid}"), RequirePermission(PermissionKeys.ChannelsManage)]
    public async Task<ActionResult<SalesChannelDto>> Update(Guid id, UpsertSalesChannelRequest request, CancellationToken ct) => Ok(await service.UpdateAsync(id, request, ct));
    [HttpDelete("{id:guid}"), RequirePermission(PermissionKeys.ChannelsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
    [HttpGet("{id:guid}/prices"), RequirePermission(PermissionKeys.ChannelsManage)]
    public async Task<ActionResult<List<ProductChannelPriceDto>>> Prices(Guid id, CancellationToken ct) => Ok(await service.GetPricesAsync(id, ct));
    [HttpGet("{id:guid}/catalog-prices"), RequirePermission(PermissionKeys.SalesCreate)]
    public async Task<ActionResult<List<ProductChannelPriceDto>>> CatalogPrices(Guid id, CancellationToken ct) => Ok(await service.GetPricesAsync(id, ct));
    [HttpPut("{id:guid}/prices"), RequirePermission(PermissionKeys.ChannelsManage)]
    public async Task<IActionResult> SetPrices(Guid id, SetChannelPricesRequest request, CancellationToken ct) { await service.SetPricesAsync(id, request, ct); return NoContent(); }
    [HttpGet("branches/{branchId:guid}"), RequirePermission(PermissionKeys.ChannelsManage)]public async Task<ActionResult<List<BranchChannelAvailabilityDto>>>Availability(Guid branchId,CancellationToken ct)=>Ok(await service.GetAvailabilityAsync(branchId,ct));
    [HttpPut("{id:guid}/branches/{branchId:guid}"), RequirePermission(PermissionKeys.ChannelsManage)]public async Task<ActionResult<BranchChannelAvailabilityDto>>SetAvailability(Guid id,Guid branchId,SetBranchChannelAvailabilityRequest request,CancellationToken ct)=>Ok(await service.SetAvailabilityAsync(branchId,id,request,ct));
}
