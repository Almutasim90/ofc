using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.API.Authorization;
using POS.Application.RestaurantCatalog;
using POS.Domain.Constants;

namespace POS.API.Controllers;

[ApiController, Route("api/restaurant-catalog"), Authorize]
public class RestaurantCatalogController(RestaurantCatalogService service) : ControllerBase
{
    [HttpGet("tables")] public async Task<ActionResult<List<RestaurantTableDto>>> Tables([FromQuery] Guid branchId, CancellationToken ct) => Ok(await service.GetTablesAsync(branchId, ct));
    [HttpPost("tables"), RequirePermission(PermissionKeys.TablesManage)] public async Task<ActionResult<RestaurantTableDto>> CreateTable(SaveRestaurantTableRequest request, CancellationToken ct) => Ok(await service.SaveTableAsync(null, request, ct));
    [HttpPut("tables/{id:guid}"), RequirePermission(PermissionKeys.TablesManage)] public async Task<ActionResult<RestaurantTableDto>> UpdateTable(Guid id, SaveRestaurantTableRequest request, CancellationToken ct) => Ok(await service.SaveTableAsync(id, request, ct));
    [HttpGet("branches/{branchId:guid}/features")] public async Task<ActionResult<List<BranchFeatureFlagDto>>> Features(Guid branchId, CancellationToken ct) => Ok(await service.GetFlagsAsync(branchId, ct));
    [HttpPut("branches/{branchId:guid}/features/{key}"), RequirePermission(PermissionKeys.BranchesManage)] public async Task<ActionResult<BranchFeatureFlagDto>> SetFeature(Guid branchId, string key, SetFeatureFlagRequest request, CancellationToken ct) => Ok(await service.SetFlagAsync(branchId, key, request.IsEnabled, ct));

    [HttpGet("categories")] public async Task<ActionResult<List<MenuCategoryDto>>> Categories([FromQuery] Guid? branchId, CancellationToken ct) => Ok(await service.GetCategoriesAsync(branchId, ct));
    [HttpPost("categories"), RequirePermission(PermissionKeys.ProductsManage)] public async Task<ActionResult<MenuCategoryDto>> CreateCategory(SaveMenuCategoryRequest request, CancellationToken ct) => Ok(await service.SaveCategoryAsync(null, request, ct));
    [HttpPut("categories/{id:guid}"), RequirePermission(PermissionKeys.ProductsManage)] public async Task<ActionResult<MenuCategoryDto>> UpdateCategory(Guid id, SaveMenuCategoryRequest request, CancellationToken ct) => Ok(await service.SaveCategoryAsync(id, request, ct));
    [HttpPut("categories/reorder"), RequirePermission(PermissionKeys.ProductsManage)] public async Task<IActionResult> Reorder(ReorderCategoriesRequest request, CancellationToken ct) { await service.ReorderCategoriesAsync(request.CategoryIds, ct); return NoContent(); }
    [HttpPut("categories/{categoryId:guid}/branches/{branchId:guid}"), RequirePermission(PermissionKeys.ProductsManage)] public async Task<IActionResult> Availability(Guid categoryId, Guid branchId, SetCategoryAvailabilityRequest request, CancellationToken ct) { await service.SetCategoryAvailabilityAsync(categoryId, branchId, request.IsAvailable, ct); return NoContent(); }

    [HttpGet("items")] public async Task<ActionResult<List<MenuItemDto>>> Items([FromQuery] Guid? categoryId, CancellationToken ct) => Ok(await service.GetItemsAsync(categoryId, ct));
    [HttpPost("items"), RequirePermission(PermissionKeys.ProductsManage)] public async Task<ActionResult<MenuItemDto>> CreateItem(SaveMenuItemRequest request, CancellationToken ct) => Ok(await service.SaveItemAsync(null, request, ct));
    [HttpPut("items/{id:guid}"), RequirePermission(PermissionKeys.ProductsManage)] public async Task<ActionResult<MenuItemDto>> UpdateItem(Guid id, SaveMenuItemRequest request, CancellationToken ct) => Ok(await service.SaveItemAsync(id, request, ct));
    [HttpGet("combos/{id:guid}")] public async Task<ActionResult<List<ComboComponentDto>>> Combo(Guid id, CancellationToken ct) => Ok(await service.GetComboAsync(id, ct));
    [HttpPut("combos/{id:guid}"), RequirePermission(PermissionKeys.CombosManage)] public async Task<IActionResult> SaveCombo(Guid id, SaveComboDefinitionRequest request, CancellationToken ct) { await service.SaveComboAsync(id, request, ct); return NoContent(); }
}
