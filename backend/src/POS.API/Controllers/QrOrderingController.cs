using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using POS.API.Hubs;
using POS.Application.Orders;
using POS.Application.QrOrdering;
namespace POS.API.Controllers;

[ApiController, Route("api/qr-ordering"), EnableRateLimiting("qr")]
public class QrOrderingController(QrOrderingService service, IHubContext<RestaurantOrdersHub> staffHub, IHubContext<QrOrdersHub> qrHub) : ControllerBase
{
    [HttpGet("resolve/{token}"), AllowAnonymous] public async Task<ActionResult<QrSessionDto>> Resolve(string token, CancellationToken ct) => Ok(await service.ResolveAsync(token, ct));
    [HttpGet("points/{pointId:guid}/resolve"), AllowAnonymous] public async Task<ActionResult<QrSessionDto>> ResolvePoint(Guid pointId, [FromQuery] string token, CancellationToken ct) => Ok(await service.ResolveSignedAsync(pointId, token, ct));
    [HttpGet("sessions/{sessionId:guid}/menu"), AllowAnonymous] public async Task<ActionResult<List<QrMenuCategoryDto>>> Menu(Guid sessionId, [FromHeader(Name = "X-QR-Session")] string accessToken, CancellationToken ct) => Ok(await service.GetMenuAsync(sessionId, accessToken, ct));
    [HttpGet("sessions/{sessionId:guid}/order"), AllowAnonymous] public async Task<ActionResult<RestaurantOrderDto>> Order(Guid sessionId, [FromHeader(Name = "X-QR-Session")] string accessToken, CancellationToken ct) => Ok(await service.GetSessionOrderAsync(sessionId, accessToken, ct));
    [HttpPost("sessions/{sessionId:guid}/orders"), AllowAnonymous] public async Task<ActionResult<RestaurantOrderDto>> Add(Guid sessionId, AddQrOrderRequest request, [FromHeader(Name = "X-QR-Session")] string? accessToken, CancellationToken ct) => Ok(await service.AddAsync(sessionId, request with { AccessToken = accessToken ?? request.AccessToken }, ct));
    [HttpPost("orders/{orderId:guid}/confirm"), AllowAnonymous] public async Task<IActionResult> Confirm(Guid orderId, ConfirmQrOrderRequest request, [FromHeader(Name = "X-QR-Session")] string? accessToken, CancellationToken ct) { var effective = accessToken ?? request.AccessToken; await service.ConfirmAsync(orderId, request with { AccessToken = effective }, ct); var order = await service.GetSessionOrderAsync(request.SessionId, effective ?? request.QrCodeToken ?? string.Empty, ct); await staffHub.Clients.Group(RestaurantOrdersHub.BranchGroup(order.BranchId)).SendAsync("QrOrderPendingApproval", order, ct); await qrHub.Clients.Group(QrOrdersHub.SessionGroup(request.SessionId)).SendAsync("QrOrderStatusChanged", order, ct); return NoContent(); }
}
