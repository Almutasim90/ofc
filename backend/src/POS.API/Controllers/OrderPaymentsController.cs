using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using POS.API.Authorization;using POS.Application.Payments;using POS.Domain.Constants;
namespace POS.API.Controllers;
[ApiController,Route("api/order-payments"),Authorize]
public class OrderPaymentsController(OrderPaymentService service):ControllerBase
{
 [HttpGet("methods")]public async Task<ActionResult<List<PaymentMethodDto>>>Methods(CancellationToken ct)=>Ok(await service.MethodsAsync(ct));
 [HttpGet("{orderId:guid}")]public async Task<ActionResult<List<OrderPaymentDto>>>Payments(Guid orderId,CancellationToken ct)=>Ok(await service.PaymentsAsync(orderId,ct));
 [HttpPost("{orderId:guid}"),RequirePermission(PermissionKeys.OrdersCreate)]public async Task<ActionResult<OrderPaymentDto>>Record(Guid orderId,RecordOrderPaymentRequest request,CancellationToken ct)=>Ok(await service.RecordAsync(orderId,request,ct));
 [HttpGet("{orderId:guid}/edits")]public async Task<ActionResult<List<OrderEditLogDto>>>Edits(Guid orderId,CancellationToken ct)=>Ok(await service.EditsAsync(orderId,ct));
 [HttpPost("{orderId:guid}/edits"),RequirePermission(PermissionKeys.ClosedOrdersEdit)]public async Task<ActionResult<OrderEditLogDto>>Edit(Guid orderId,EditClosedOrderRequest request,CancellationToken ct)=>Ok(await service.EditAsync(orderId,request,ct));
}
