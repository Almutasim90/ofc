using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Orders;
public class RestaurantOrderService(IAppDbContext db, ICurrentUserService currentUser)
{
    public Task<List<OrderTypeDto>> GetTypesAsync(CancellationToken ct=default)=>db.OrderTypes.OrderBy(x=>x.Code).Select(x=>new OrderTypeDto(x.Id,x.Code,x.NameAr,x.NameEn)).ToListAsync(ct);
    public async Task<RestaurantOrderDto> CreateAsync(CreateRestaurantOrderRequest request,CancellationToken ct=default)
    {
        if(request.Lines.Count==0||request.Lines.Any(x=>x.Quantity<=0))throw new ValidationException("Order needs at least one line with a positive quantity.");
        var type=await db.OrderTypes.FirstOrDefaultAsync(x=>x.Id==request.OrderTypeId,ct)??throw new NotFoundException("Order type not found.");
        if(type.Code=="DINE_IN"&&request.TableId is null)throw new ValidationException("A table is required for dine-in orders.");
        if(type.Code!="DINE_IN"&&request.TableId is not null)throw new ValidationException("A table is only valid for dine-in orders.");
        if(type.Code=="CAR_PICKUP")ValidateCarPickup(request.CarPlateNumber,await db.BranchFeatureFlags.AnyAsync(x=>x.BranchId==request.BranchId&&x.FeatureKey=="CAR_PICKUP"&&x.IsEnabled,ct));
        if(request.TableId is not null&&!await db.RestaurantTables.AnyAsync(x=>x.Id==request.TableId&&x.BranchId==request.BranchId&&x.IsActive,ct))throw new ValidationException("The table is unavailable at this branch.");
        var ids=request.Lines.Select(x=>x.MenuItemId).Distinct().ToList(); var items=await db.MenuItems.Where(x=>ids.Contains(x.Id)&&x.IsActive).Include(x=>x.ModifierGroups).ThenInclude(x=>x.ModifierGroup).ThenInclude(x=>x.Options).Include(x=>x.ComboComponents).ThenInclude(x=>x.Options).ToDictionaryAsync(x=>x.Id,ct);
        if(items.Count!=ids.Count)throw new ValidationException("One or more menu items are unavailable.");
        var branch=await db.Branches.FirstOrDefaultAsync(x=>x.Id==request.BranchId&&x.IsActive,ct)??throw new NotFoundException("Branch not found.");
        var order=new RestaurantOrder{Id=Guid.NewGuid(),BranchId=request.BranchId,OrderNumber=await db.ClaimNextOrderNumberAsync(request.BranchId,ct),OrderTypeId=type.Id,TableId=request.TableId,CarPlateNumber=request.CarPlateNumber?.Trim(),CashierUserId=currentUser.UserId??throw new ValidationException("Authenticated user is required."),CashShiftId=await db.CashShifts.Where(x=>x.BranchId==request.BranchId&&x.Status==CashShiftStatuses.Open).Select(x=>(Guid?)x.Id).FirstOrDefaultAsync(ct),BusinessDate=DateOnly.FromDateTime(DateTime.UtcNow),CreatedAt=DateTime.UtcNow,DiscountAmount=request.DiscountAmount,Status=RestaurantOrderStatuses.Open};
        foreach(var input in request.Lines)
        {
            var item=items[input.MenuItemId]; decimal delta=0; var line=new RestaurantOrderItem{Id=Guid.NewGuid(),MenuItemId=item.Id,MenuItemNameSnapshot=item.NameEn,Quantity=input.Quantity,Notes=input.Notes?.Trim()};
            if(item.Kind==MenuItemKinds.SingleProduct){var selected=input.ModifierOptionIds.Distinct().ToList();if(selected.Count!=input.ModifierOptionIds.Count)throw new ValidationException("Duplicate modifier selection.");foreach(var link in item.ModifierGroups){var group=link.ModifierGroup;var chosen=group.Options.Where(x=>selected.Contains(x.Id)&&x.IsActive).ToList();var min=group.IsRequired?Math.Max(1,group.MinSelect):group.MinSelect;if(chosen.Count<min||chosen.Count>group.MaxSelect)throw new ValidationException($"Invalid selection for {group.NameEn}.");foreach(var option in chosen){delta+=option.PriceDelta;line.Modifiers.Add(new(){Id=Guid.NewGuid(),ModifierOptionId=option.Id,PriceDeltaSnapshot=option.PriceDelta});}}if(selected.Any(x=>!line.Modifiers.Any(m=>m.ModifierOptionId==x)))throw new ValidationException("Modifier is unavailable for this product.");}
            else {foreach(var component in item.ComboComponents){var chosen=input.ComboSelections.Where(x=>x.ComboComponentId==component.Id).ToList();var min=component.IsRequired?Math.Max(1,component.MinSelect):component.MinSelect;if(chosen.Count<min||chosen.Count>component.MaxSelect)throw new ValidationException($"Invalid selection for {component.SlotLabel}.");foreach(var selection in chosen){var option=component.Options.SingleOrDefault(x=>x.MenuItemId==selection.SelectedMenuItemId)??throw new ValidationException("Combo option is unavailable.");delta+=option.PriceDelta;line.ComboSelections.Add(new(){Id=Guid.NewGuid(),ComboComponentId=component.Id,SelectedMenuItemId=option.MenuItemId,PriceDeltaSnapshot=option.PriceDelta});}}}
            line.UnitPriceSnapshot=item.BasePrice+delta;line.LineTotal=line.UnitPriceSnapshot*line.Quantity;order.Items.Add(line);
        }
        order.Subtotal=order.Items.Sum(x=>x.LineTotal);if(request.DiscountAmount<0||request.DiscountAmount>order.Subtotal)throw new ValidationException("Invalid discount.");order.GrandTotal=order.Subtotal-order.DiscountAmount;db.RestaurantOrders.Add(order);await db.SaveChangesAsync(ct);return ToDto(order,type.Code,request.TableId is null?null:(await db.RestaurantTables.FirstAsync(x=>x.Id==request.TableId,ct)).Label);
    }
    public Task<List<RestaurantOrderDto>> GetAsync(Guid branchId,CancellationToken ct=default)=>db.RestaurantOrders.Where(x=>x.BranchId==branchId).OrderByDescending(x=>x.CreatedAt).Take(100).Select(x=>new RestaurantOrderDto(x.Id,x.BranchId,x.OrderNumber,x.OrderType.Code,x.TableId,x.Table==null?null:x.Table.Label,x.Status,x.Subtotal,x.DiscountAmount,x.GrandTotal,x.CreatedAt,x.Items.Select(i=>new OrderLineDto(i.Id,i.MenuItemId,i.MenuItemNameSnapshot,i.UnitPriceSnapshot,i.Quantity,i.LineTotal,i.Notes,i.IsCancelled,i.Modifiers.Select(m=>m.ModifierOptionId).ToList(),i.ComboSelections.Select(c=>c.SelectedMenuItemId).ToList())).ToList())).ToListAsync(ct);
    private static RestaurantOrderDto ToDto(RestaurantOrder x,string code,string? table)=>new(x.Id,x.BranchId,x.OrderNumber,code,x.TableId,table,x.Status,x.Subtotal,x.DiscountAmount,x.GrandTotal,x.CreatedAt,x.Items.Select(i=>new OrderLineDto(i.Id,i.MenuItemId,i.MenuItemNameSnapshot,i.UnitPriceSnapshot,i.Quantity,i.LineTotal,i.Notes,i.IsCancelled,i.Modifiers.Select(m=>m.ModifierOptionId).ToList(),i.ComboSelections.Select(c=>c.SelectedMenuItemId).ToList())).ToList());
    public static void ValidateCarPickup(string? plate,bool enabled){if(!enabled)throw new ValidationException("Car pickup is disabled for this branch.");if(string.IsNullOrWhiteSpace(plate))throw new ValidationException("Car plate number is required.");if(plate.Trim().Length>30)throw new ValidationException("Car plate number is too long.");}
}
