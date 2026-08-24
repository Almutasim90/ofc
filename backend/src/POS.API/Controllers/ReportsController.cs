using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Reports;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController(ReportService reportService) : ControllerBase
{
    [HttpGet("dashboard")]
    [RequirePermission(PermissionKeys.ReportsBranchView)]
    public async Task<ActionResult<ManagerDashboardDto>> Dashboard(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] Guid? branchId,
        CancellationToken cancellationToken) =>
        Ok(await reportService.GetDashboardAsync(from, to, branchId, cancellationToken));

    [HttpGet("daily")]
    [RequirePermission(PermissionKeys.ReportsBranchView)]
    public async Task<ActionResult<DailySalesReportDto>> Daily(
        [FromQuery] Guid branchId, [FromQuery] DateOnly date, CancellationToken cancellationToken) =>
        Ok(await reportService.GetDailyBranchAsync(branchId, date, cancellationToken));

    [HttpGet("global")]
    [RequirePermission(PermissionKeys.ReportsGlobalView)]
    public async Task<ActionResult<GlobalSalesReportDto>> Global(
        [FromQuery] DateOnly date, CancellationToken cancellationToken) =>
        Ok(await reportService.GetGlobalAsync(date, cancellationToken));

    [HttpGet("shifts/{shiftId:guid}/inventory-consumption")]
    [RequirePermission(PermissionKeys.ReportsBranchView)]
    public async Task<ActionResult<ShiftInventoryReportDto>> ShiftInventory(
        Guid shiftId, CancellationToken cancellationToken) =>
        Ok(await reportService.GetShiftInventoryAsync(shiftId, cancellationToken));

    [HttpGet("discounts")]
    [RequirePermission(PermissionKeys.ReportsBranchView)]
    public async Task<ActionResult<DiscountReportDto>> Discounts([FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] Guid? branchId, [FromQuery] Guid? cashierUserId, CancellationToken cancellationToken) =>
        Ok(await reportService.GetDiscountsAsync(from, to, branchId, cashierUserId, cancellationToken));

    [HttpGet("channels")]
    [RequirePermission(PermissionKeys.ReportsBranchView)]
    public async Task<ActionResult<List<ChannelSalesDto>>> Channels([FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] Guid? branchId, CancellationToken cancellationToken) =>
        Ok(await reportService.GetChannelDistributionAsync(from, to, branchId, cancellationToken));
}
