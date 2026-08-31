using Microsoft.AspNetCore.Authorization;using Microsoft.AspNetCore.Mvc;using POS.API.Authorization;using POS.Application.RestaurantInventory;using POS.Domain.Constants;
namespace POS.API.Controllers;
[ApiController,Route("api/restaurant-inventory"),Authorize,RequirePermission(PermissionKeys.InventoryAdjust)]
public class RestaurantInventoryController(RestaurantInventoryService service):ControllerBase
{
 [HttpGet("units")]public async Task<IActionResult>Units(CancellationToken ct)=>Ok(await service.Units(ct));[HttpPost("units")]public async Task<IActionResult>Unit(UnitInput r,CancellationToken ct)=>Ok(await service.SaveUnit(r.Name,r.Symbol,r.IsBase,ct));
 [HttpGet("ingredients")]public async Task<IActionResult>Ingredients(CancellationToken ct)=>Ok(await service.Ingredients(ct));[HttpPost("ingredients")]public async Task<IActionResult>Ingredient(IngredientInput r,CancellationToken ct)=>Ok(await service.SaveIngredient(r.NameAr,r.NameEn,r.UnitOfMeasureId,ct));
 [HttpGet("warehouses")]public async Task<IActionResult>Warehouses([FromQuery]Guid branchId,CancellationToken ct)=>Ok(await service.Warehouses(branchId,ct));[HttpPost("warehouses")]public async Task<IActionResult>Warehouse(WarehouseInput r,CancellationToken ct)=>Ok(await service.SaveWarehouse(r.BranchId,r.NameAr,r.NameEn,r.IsDefault,ct));
 [HttpGet("reasons")]public async Task<IActionResult>Reasons(CancellationToken ct)=>Ok(await service.Reasons(ct));[HttpGet("stock")]public async Task<IActionResult>Stock([FromQuery]Guid warehouseId,CancellationToken ct)=>Ok(await service.Stock(warehouseId,ct));[HttpPost("movements")]public async Task<IActionResult>Move(StockMovementRequest r,CancellationToken ct){await service.Move(r,ct);return NoContent();}
 [HttpPut("recipes/{menuItemId:guid}/branches/{branchId:guid}")]public async Task<IActionResult>Recipe(Guid menuItemId,Guid branchId,List<RecipeLineRequest> r,CancellationToken ct){await service.SaveRecipe(menuItemId,branchId,r,ct);return NoContent();}
 [HttpPost("orders/{orderId:guid}/confirm"),RequirePermission(PermissionKeys.OrdersCreate)]public async Task<IActionResult>Confirm(Guid orderId,CancellationToken ct){await service.Confirm(orderId,ct);return NoContent();}
 public record UnitInput(string Name,string Symbol,bool IsBase);public record IngredientInput(string NameAr,string NameEn,Guid UnitOfMeasureId);public record WarehouseInput(Guid BranchId,string NameAr,string NameEn,bool IsDefault);
}
