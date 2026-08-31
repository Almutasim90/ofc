using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using POS.API.Authorization;using POS.Application.Orders;using POS.Domain.Constants;
namespace POS.API.Controllers;
[ApiController,Route("api/restaurant-orders"),Authorize]
public class RestaurantOrdersController(RestaurantOrderService service):ControllerBase
{
 [HttpGet("types")]public async Task<ActionResult<List<OrderTypeDto>>>Types(CancellationToken ct)=>Ok(await service.GetTypesAsync(ct));
 [HttpGet]public async Task<ActionResult<List<RestaurantOrderDto>>>Get([FromQuery]Guid branchId,CancellationToken ct)=>Ok(await service.GetAsync(branchId,ct));
 [HttpPost,RequirePermission(PermissionKeys.OrdersCreate)]public async Task<ActionResult<RestaurantOrderDto>>Create(CreateRestaurantOrderRequest request,CancellationToken ct)=>Ok(await service.CreateAsync(request,ct));
}
