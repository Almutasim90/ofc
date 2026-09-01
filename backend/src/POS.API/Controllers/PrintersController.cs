using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Printing;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController, Route("api/printers"), Authorize, RequirePermission(PermissionKeys.PrintingManage)]
public class PrintersController(PrinterAdminService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<List<PrinterConfigDto>>> Get([FromQuery] Guid branchId, CancellationToken ct) => Ok(await service.GetConfigsAsync(branchId, ct));
    [HttpPost] public async Task<ActionResult<PrinterConfigDto>> Create(SavePrinterConfigRequest request, CancellationToken ct) => Ok(await service.SaveConfigAsync(null, request, ct));
    [HttpPut("{id:guid}")] public async Task<ActionResult<PrinterConfigDto>> Update(Guid id, SavePrinterConfigRequest request, CancellationToken ct) => Ok(await service.SaveConfigAsync(id, request, ct));
    [HttpPost("{id:guid}/test")] public async Task<IActionResult> Test(Guid id, PrinterTestRequest request, CancellationToken ct) { await service.TestAsync(id, request.Text, ct); return NoContent(); }
    [HttpGet("sections")] public async Task<ActionResult<List<PrinterSectionDto>>> Sections([FromQuery] Guid branchId, CancellationToken ct) => Ok(await service.GetSectionsAsync(branchId, ct));
    [HttpPost("sections")] public async Task<ActionResult<PrinterSectionDto>> CreateSection(SavePrinterSectionRequest request, CancellationToken ct) => Ok(await service.SaveSectionAsync(null, request, ct));
    [HttpPut("sections/{id:guid}")] public async Task<ActionResult<PrinterSectionDto>> UpdateSection(Guid id, SavePrinterSectionRequest request, CancellationToken ct) => Ok(await service.SaveSectionAsync(id, request, ct));
}
