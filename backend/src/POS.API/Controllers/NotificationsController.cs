using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Notifications;
using POS.Domain.Constants;

namespace POS.API.Controllers;
[ApiController, Route("api/notifications"), RequirePermission(PermissionKeys.InventoryAdjust)]
public class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet("low-stock")]
    public async Task<ActionResult<List<LowStockNotificationDto>>> Get([FromQuery] bool includeResolved, CancellationToken ct) => Ok(await service.GetAsync(includeResolved, ct));
}
