using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Ofc;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController(CategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetAllAsync(cancellationToken));
    }

    /// <summary>The menu as it should render for one branch - fail-open per
    /// OFC-System-Detailed-Spec.md section 1.1, so a branch with no explicit toggles sees
    /// every active category.</summary>
    [HttpGet("available")]
    public async Task<ActionResult<List<CategoryDto>>> GetAvailableForBranch([FromQuery] Guid branchId, CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetAvailableForBranchAsync(branchId, cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = category.Id }, category);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<ActionResult<CategoryDto>> Update(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        return Ok(await categoryService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/branch-availability")]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<ActionResult<List<CategoryBranchAvailabilityDto>>> GetBranchAvailability(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await categoryService.GetBranchAvailabilityAsync(id, cancellationToken));
    }

    [HttpPut("{id:guid}/branch-availability")]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> SetBranchAvailability(Guid id, SetCategoryBranchAvailabilityRequest request, CancellationToken cancellationToken)
    {
        await categoryService.SetBranchAvailabilityAsync(id, request, cancellationToken);
        return NoContent();
    }
}
