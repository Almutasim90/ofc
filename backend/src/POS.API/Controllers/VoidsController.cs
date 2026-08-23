using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Shifts;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/sales/{saleId:guid}/void")]
[RequirePermission(PermissionKeys.SalesVoid)]
public class VoidsController(VoidService voidService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<VoidRequestDto>> Void(Guid saleId, VoidSaleRequest request, CancellationToken cancellationToken)
        => Ok(await voidService.VoidAsync(saleId, request, cancellationToken));
}
