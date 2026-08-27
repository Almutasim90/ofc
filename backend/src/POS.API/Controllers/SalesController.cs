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
    [HttpGet]
    [RequirePermission(PermissionKeys.SalesEdit)]
    public async Task<ActionResult<List<SaleDto>>> List([FromQuery] Guid branchId, CancellationToken cancellationToken)
        => Ok(await saleService.ListAsync(branchId, cancellationToken));

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.SalesEdit)]
    public async Task<ActionResult<SaleDto>> Update(Guid id, UpdateSaleRequest request, CancellationToken cancellationToken)
        => Ok(await saleService.UpdateAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}/history")]
    [RequirePermission(PermissionKeys.SalesEdit)]
    public async Task<ActionResult<List<SaleEditDto>>> History(Guid id, CancellationToken cancellationToken)
        => Ok(await saleService.HistoryAsync(id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<SaleDto>> Create(CreateSaleRequest request, CancellationToken cancellationToken)
    {
        var sale = await saleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Create), new { id = sale.Id }, sale);
    }
}
