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

    [HttpGet("supply-packages")]
    public async Task<ActionResult<List<SupplyPackageDto>>> Packages([FromQuery] Guid? rawMaterialId, CancellationToken ct) =>
        Ok(await stockService.GetSupplyPackagesAsync(rawMaterialId, ct));

    [HttpPost("supply-packages")]
    public async Task<ActionResult<SupplyPackageDto>> CreatePackage(UpsertSupplyPackageRequest request, CancellationToken ct)
    {
        var item = await stockService.CreateSupplyPackageAsync(request, ct);
        return Created($"api/inventory/supply-packages/{item.Id}", item);
    }

    [HttpPost("receipts")]
    public async Task<ActionResult<StockReceiptDto>> Receive(ReceiveStockRequest request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");
        return Ok(await stockService.ReceiveAsync(request, userId, ct));
    }

    [HttpGet("receipts")]
    public async Task<ActionResult<List<StockReceiptDto>>> Receipts([FromQuery] Guid branchId, CancellationToken ct) =>
        Ok(await stockService.GetRecentReceiptsAsync(branchId, ct));

    [HttpPost("items")]
    public async Task<ActionResult<CreateInventoryItemResult>> CreateItem(CreateInventoryItemRequest request, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Missing user context.");
        var result = await stockService.CreateInventoryItemAsync(request, userId, ct);
        return Created($"api/inventory/items/{result.RawMaterialId}", result);
    }
}
