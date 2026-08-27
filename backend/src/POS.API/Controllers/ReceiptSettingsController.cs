using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Settings;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController, Route("api/receipt-settings"), Authorize]
public class ReceiptSettingsController(ReceiptSettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReceiptSettingsDto>> Get(CancellationToken ct) => Ok(await service.GetAsync(ct));

    [HttpPut, RequirePermission(PermissionKeys.ReceiptManage)]
    public async Task<ActionResult<ReceiptSettingsDto>> Save(UpdateReceiptSettingsRequest request, CancellationToken ct) =>
        Ok(await service.SaveAsync(request, ct));
}
