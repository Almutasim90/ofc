using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using POS.Application.Orders;
using POS.Application.QrOrdering;
namespace POS.API.Controllers;

[ApiController, Route("api/qr-ordering"), EnableRateLimiting("qr")]
public class QrOrderingController(QrOrderingService service) : ControllerBase
{
    [HttpGet("resolve/{token}"), AllowAnonymous] public async Task<ActionResult<QrSessionDto>> Resolve(string token, CancellationToken ct) => Ok(await service.ResolveAsync(token, ct));
    [HttpPost("sessions/{sessionId:guid}/orders"), AllowAnonymous] public async Task<ActionResult<RestaurantOrderDto>> Add(Guid sessionId, AddQrOrderRequest request, CancellationToken ct) => Ok(await service.AddAsync(sessionId, request, ct));
    [HttpPost("orders/{orderId:guid}/confirm"), AllowAnonymous] public async Task<IActionResult> Confirm(Guid orderId, ConfirmQrOrderRequest request, CancellationToken ct) { await service.ConfirmAsync(orderId, request, ct); return NoContent(); }
}
