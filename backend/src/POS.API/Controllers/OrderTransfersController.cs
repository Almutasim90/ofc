using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Abstractions;
using POS.Application.QrOrdering;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController, Route("api/orders"), Authorize]
public class OrderTransfersController(QrOrderingService service, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("{orderId:guid}/transfer"), RequirePermission(PermissionKeys.OrdersTransfer)]
    public async Task<IActionResult> Transfer(Guid orderId, TransferOrderRequest request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Authenticated user is required.");
        await service.TransferOrderAsync(orderId, request.NewOrderingPointId, userId, request.Notes, ct);
        return NoContent();
    }
}
