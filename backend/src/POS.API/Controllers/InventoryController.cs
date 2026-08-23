using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Inventory;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/inventory")]
[RequirePermission(PermissionKeys.InventoryAdjust)]
public class InventoryController(StockService stockService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("stock")]
    public async Task<ActionResult<List<StockStatusDto>>> GetStock([FromQuery] Guid branchId, CancellationToken cancellationToken)
    {
        return Ok(await stockService.GetStatusAsync(branchId, cancellationToken));
    }

    [HttpPost("stock-adjustments")]
    public async Task<IActionResult> Adjust(AdjustStockRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");
        await stockService.AdjustAsync(request, userId, cancellationToken);
        return NoContent();
    }

    [HttpPut("stock/low-stock-threshold")]
    public async Task<IActionResult> SetLowStockThreshold(SetLowStockThresholdRequest request, CancellationToken cancellationToken)
    {
        await stockService.SetLowStockThresholdAsync(request, cancellationToken);
        return NoContent();
    }
}
