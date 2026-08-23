using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Catalog;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/branches")]
[Authorize]
public class BranchesController(BranchService branchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BranchDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await branchService.GetAllAsync(cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.BranchesManage)]
    public async Task<ActionResult<BranchDto>> Create(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var branch = await branchService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = branch.Id }, branch);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.BranchesManage)]
    public async Task<ActionResult<BranchDto>> Update(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        return Ok(await branchService.UpdateAsync(id, request, cancellationToken));
    }
}
