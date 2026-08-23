using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.Catalog;
using POS.Application.Inventory;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController(ProductService productService, RecipeService recipeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await productService.GetAllAsync(cancellationToken));
    }

    [HttpPost]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await productService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = product.Id }, product);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<ActionResult<ProductDto>> Update(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        return Ok(await productService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpGet("{id:guid}/recipe")]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<ActionResult<List<RecipeLineDto>>> GetRecipe(Guid id, [FromQuery] Guid branchId, CancellationToken cancellationToken)
    {
        return Ok(await recipeService.GetAsync(id, branchId, cancellationToken));
    }

    [HttpPut("{id:guid}/recipe")]
    [RequirePermission(PermissionKeys.ProductsManage)]
    public async Task<IActionResult> SetRecipe(Guid id, SetRecipeRequest request, CancellationToken cancellationToken)
    {
        await recipeService.SetAsync(id, request, cancellationToken);
        return NoContent();
    }
}
