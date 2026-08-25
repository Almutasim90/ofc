using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Settings;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController, Route("api/email-settings"), RequirePermission(PermissionKeys.EmailManage)]
public class EmailSettingsController(EmailSettingsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EmailSettingsDto>> Get(CancellationToken ct) => Ok(await service.GetAsync(ct));

    [HttpPut]
    public async Task<ActionResult<EmailSettingsDto>> Save(UpdateEmailSettingsRequest request, CancellationToken ct) =>
        Ok(await service.SaveAsync(request, ct));

    [HttpPost("test")]
    public async Task<IActionResult> Test(TestEmailRequest request, CancellationToken ct)
    {
        await service.SendTestAsync(request.Recipient, ct);
        return NoContent();
    }
}
