using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using POS.API.Authorization;using POS.Application.Orders;using POS.Application.Printing;using POS.Domain.Constants;
namespace POS.API.Controllers;
[ApiController,Route("api/restaurant-orders"),Authorize]
public class RestaurantOrdersController(RestaurantOrderService service,OrderCancellationService cancellations,OrderPrintingService printing):ControllerBase
{
 [HttpGet("types")]public async Task<ActionResult<List<OrderTypeDto>>>Types(CancellationToken ct)=>Ok(await service.GetTypesAsync(ct));
 [HttpGet]public async Task<ActionResult<List<RestaurantOrderDto>>>Get([FromQuery]Guid branchId,CancellationToken ct)=>Ok(await service.GetAsync(branchId,ct));
 [HttpPost,RequirePermission(PermissionKeys.OrdersCreate)]public async Task<ActionResult<RestaurantOrderDto>>Create(CreateRestaurantOrderRequest request,CancellationToken ct)=>Ok(await service.CreateAsync(request,ct));
 [HttpPost("{orderId:guid}/confirm"),RequirePermission(PermissionKeys.OrdersCreate)]public async Task<IActionResult>Confirm(Guid orderId,CancellationToken ct){await printing.ConfirmAndPrintAsync(orderId,ct);return NoContent();}
 [HttpPost("{orderId:guid}/items/{itemId:guid}/cancel"),RequirePermission(PermissionKeys.OrdersCancel)]public async Task<IActionResult>CancelItem(Guid orderId,Guid itemId,CancelOrderRequest request,CancellationToken ct){await cancellations.CancelItemAsync(orderId,itemId,request.Reason,ct);return NoContent();}
 [HttpPost("{orderId:guid}/cancel"),RequirePermission(PermissionKeys.OrdersCancel)]public async Task<IActionResult>CancelOrder(Guid orderId,CancelOrderRequest request,CancellationToken ct){await cancellations.CancelOrderAsync(orderId,request.Reason,ct);return NoContent();}
 [HttpGet("cancellations"),RequirePermission(PermissionKeys.OrdersCancel)]public async Task<ActionResult<List<OrderCancellationDto>>>CancellationLog([FromQuery]Guid branchId,[FromQuery]DateTime? from,[FromQuery]DateTime? to,[FromQuery]Guid? cashierUserId,CancellationToken ct)=>Ok(await cancellations.GetAsync(branchId,from,to,cashierUserId,ct));
}
