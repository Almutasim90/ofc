using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Sales;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/sales")]
[RequirePermission(PermissionKeys.SalesCreate)]
public class SalesController(SaleService saleService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SaleDto>> Create(CreateSaleRequest request, CancellationToken cancellationToken)
    {
        var sale = await saleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Create), new { id = sale.Id }, sale);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SaleDto>>> ListForShift([FromQuery] Guid shiftId, CancellationToken cancellationToken) =>
        Ok(await saleService.ListForShiftAsync(shiftId, cancellationToken));
}
