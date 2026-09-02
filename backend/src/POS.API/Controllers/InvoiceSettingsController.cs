using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Invoices;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController, Route("api/invoice-settings"), Authorize]
public class InvoiceSettingsController(InvoiceSettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<InvoiceSettingsDto>> Get([FromQuery] Guid branchId, CancellationToken ct) => Ok(await service.GetAsync(branchId, ct));

    [HttpPut, RequirePermission(PermissionKeys.InvoiceManage)]
    public async Task<ActionResult<InvoiceSettingsDto>> Save(SaveInvoiceSettingsRequest request, CancellationToken ct) => Ok(await service.SaveAsync(request, ct));
}
