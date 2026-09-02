using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Infrastructure.Persistence.Seed;

public static class SeedData
{
    public const string BootstrapAdminUsername = "admin";
    public const string BootstrapAdminPassword = "Admin@12345";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher passwordHasher, CancellationToken cancellationToken = default)
    {
        var existingPermissionKeys = await db.Permissions.Select(p => p.Key).ToListAsync(cancellationToken);
        var missingPermissionKeys = PermissionKeys.All.Except(existingPermissionKeys).ToList();
        if (missingPermissionKeys.Count > 0)
        {
            db.Permissions.AddRange(missingPermissionKeys.Select(key => new Permission
            {
                Id = Guid.NewGuid(),
                Key = key,
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        if (!await db.SalesChannels.AnyAsync(c => c.IsInStore, cancellationToken))
        {
            db.SalesChannels.Add(new SalesChannel { Id = SalesChannelIds.InStore, Code="IN_STORE",NameAr = "المحل", NameEn = "In-store", IsActive = true, IsInStore = true });
            await db.SaveChangesAsync(cancellationToken);
        }
        if (!await db.SalesChannels.AnyAsync(c => c.Code == "QR_TABLE", cancellationToken)) db.SalesChannels.Add(new SalesChannel { Id = SalesChannelIds.QrTable, Code = "QR_TABLE", NameAr = "طلب الطاولة QR", NameEn = "QR table", IsActive = true });
        if (!await db.SalesChannels.AnyAsync(c => c.Code == "QR_CAR", cancellationToken)) db.SalesChannels.Add(new SalesChannel { Id = SalesChannelIds.QrCar, Code = "QR_CAR", NameAr = "طلب السيارة QR", NameEn = "QR car", IsActive = true });
        await db.SaveChangesAsync(cancellationToken);

        var branches = await db.Branches.IgnoreQueryFilters().Select(x => x.Id).ToListAsync(cancellationToken);
        var qrChannelIds = await db.SalesChannels.Where(x => x.Code == "QR_TABLE" || x.Code == "QR_CAR").Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var branchId in branches)
        {
            if (!await db.BranchFeatureFlags.IgnoreQueryFilters().AnyAsync(x => x.BranchId == branchId && x.FeatureKey == BranchFeatureKeys.QrOrdering, cancellationToken)) db.BranchFeatureFlags.Add(new() { Id = Guid.NewGuid(), BranchId = branchId, FeatureKey = BranchFeatureKeys.QrOrdering, IsEnabled = true });
            foreach (var channelId in qrChannelIds) if (!await db.BranchSalesChannelAvailabilities.IgnoreQueryFilters().AnyAsync(x => x.BranchId == branchId && x.SalesChannelId == channelId, cancellationToken)) db.BranchSalesChannelAvailabilities.Add(new() { Id = Guid.NewGuid(), BranchId = branchId, SalesChannelId = channelId, IsEnabled = true });
        }
        await db.SaveChangesAsync(cancellationToken);

        if (!await db.Roles.AnyAsync(cancellationToken))
        {
            db.Roles.AddRange(RoleNames.All.Select(name => new Role
            {
                Id = Guid.NewGuid(),
                Name = name,
            }));
            await db.SaveChangesAsync(cancellationToken);
        }

        var roles = await db.Roles.ToListAsync(cancellationToken);
        var permissions = await db.Permissions.ToListAsync(cancellationToken);
        var existingRolePermissions = await db.RolePermissions.Select(rp => new { rp.RoleId, rp.PermissionId }).ToListAsync(cancellationToken);
        foreach (var role in roles)
        foreach (var key in DefaultRolePermissions.ByRole[role.Name])
        {
            var permission = permissions.First(p => p.Key == key);
            if (!existingRolePermissions.Any(x => x.RoleId == role.Id && x.PermissionId == permission.Id))
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }
        await db.SaveChangesAsync(cancellationToken);

        if (!await db.Users.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            var generalManagerRole = await db.Roles
                .FirstAsync(r => r.Name == RoleNames.GeneralManager, cancellationToken);

            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                FullName = "System Administrator",
                Username = BootstrapAdminUsername,
                PasswordHash = passwordHasher.Hash(BootstrapAdminPassword),
                BranchId = null,
                RoleId = generalManagerRole.Id,
                PreferredLanguage = "ar",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });

            await db.SaveChangesAsync(cancellationToken);
        }

        await SeedDemoRestaurantDataAsync(db, cancellationToken);
    }

    /// <summary>
    /// Seeds demo restaurant data (menu catalog, addons, combos, inventory units,
    /// ingredients, warehouse stock and recipes) so the restaurant-catalog and
    /// inventory screens can be exercised. Runs only when the menu catalog is empty,
    /// so it is safe and idempotent on repeat startups.
    /// </summary>
    private static async Task SeedDemoRestaurantDataAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.MenuCategories.AnyAsync(cancellationToken)) return;

        var branch = await db.Branches.IgnoreQueryFilters().OrderBy(x => x.NameEn).FirstOrDefaultAsync(cancellationToken);
        if (branch is null)
        {
            branch = new Branch { Id = Guid.NewGuid(), Code = "DEMO", NameAr = "الفرع الرئيسي", NameEn = "Main Branch", IsActive = true, NextOrderNumber = 1 };
            db.Branches.Add(branch);
        }

        // --- Units of measure ---
        var unitKg = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Kilogram", Symbol = "kg", IsBase = true };
        var unitL = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Liter", Symbol = "L", IsBase = true };
        var unitPc = new UnitOfMeasure { Id = Guid.NewGuid(), Name = "Piece", Symbol = "pc", IsBase = true };
        db.UnitsOfMeasure.AddRange(unitKg, unitL, unitPc);

        // --- Ingredients (raw materials for recipes) ---
        var ingBeef = Ingredient("Beef", "لحم", unitKg);
        var ingBun = Ingredient("Burger bun", "خبز برجر", unitPc);
        var ingPotato = Ingredient("Potato", "بطاطس", unitKg);
        var ingChicken = Ingredient("Chicken", "دجاج", unitKg);
        var ingRice = Ingredient("Rice", "أرز", unitKg);
        var ingTomato = Ingredient("Tomato", "طماطم", unitKg);
        var ingCheese = Ingredient("Cheese", "جبن", unitKg);
        var ingCola = Ingredient("Cola syrup", "شراب كولا", unitL);
        var ingIce = Ingredient("Ice", "ثلج", unitKg);
        db.Ingredients.AddRange(ingBeef, ingBun, ingPotato, ingChicken, ingRice, ingTomato, ingCheese, ingCola, ingIce);

        // --- Menu categories ---
        var catStarters = Category("Starters", "مقبلات", 0);
        var catMains = Category("Mains", "أطباق رئيسية", 1);
        var catDrinks = Category("Drinks", "مشروبات", 2);
        var catDesserts = Category("Desserts", "حلويات", 3);
        db.MenuCategories.AddRange(catStarters, catMains, catDrinks, catDesserts);

        // --- Menu items (single products) ---
        var itemBurger = Item(catMains, "Beef burger", "برجر لحم", 3.500m, 0);
        var itemFries = Item(catStarters, "French fries", "بطاطس مقلية", 1.200m, 1);
        var itemShawarma = Item(catMains, "Chicken shawarma", "شاورما دجاج", 2.000m, 1);
        var itemChickenRice = Item(catMains, "Chicken with rice", "دجاج مع أرز", 3.000m, 2);
        var itemCola = Item(catDrinks, "Cola", "كولات", 0.500m, 0);
        var itemWater = Item(catDrinks, "Water", "ماء", 0.300m, 1);
        var itemIceCream = Item(catDesserts, "Ice cream", "آيس كريم", 1.500m, 0);
        db.MenuItems.AddRange(itemBurger, itemFries, itemShawarma, itemChickenRice, itemCola, itemWater, itemIceCream);

        // --- Addons: modifier groups + options ---
        var grpSize = Group("Size", "الحجم", 1, 1, true);
        grpSize.Options.Add(Option("Small", "صغير", -0.500m));
        grpSize.Options.Add(Option("Regular", "عادي", 0m));
        grpSize.Options.Add(Option("Large", "كبير", 0.750m));

        var grpExtras = Group("Extras", "إضافات", 0, 3, false);
        grpExtras.Options.Add(Option("Extra cheese", "جبن إضافي", 0.400m));
        grpExtras.Options.Add(Option("Extra fries", "بطاطس إضافية", 0.600m));
        grpExtras.Options.Add(Option("Add cola", "أضف كولا", 0.500m));

        var grpSauce = Group("Sauce", "الصلصة", 0, 2, false);
        grpSauce.Options.Add(Option("Garlic sauce", "صلصة ثوم", 0.150m));
        grpSauce.Options.Add(Option("Ketchup", "كاتشب", 0.100m));
        grpSauce.Options.Add(Option("Tahini", "طحينة", 0.150m));

        var grpIce = Group("Ice level", "كمية الثلج", 1, 1, true);
        grpIce.Options.Add(Option("Normal", "عادي", 0m));
        grpIce.Options.Add(Option("No ice", "بدون ثلج", 0m));
        db.ModifierGroups.AddRange(grpSize, grpExtras, grpSauce, grpIce);

        // Wire addons to items
        AttachItemModifiers(itemBurger, grpSize, grpExtras);
        AttachItemModifiers(itemShawarma, grpSize, grpSauce);
        AttachItemModifiers(itemFries, grpSize, grpExtras);
        AttachItemModifiers(itemCola, grpIce);
        AttachItemModifiers(itemWater, grpIce);

        // --- Combo: a family combo composed of mains + drinks ---
        var combo = new MenuItem { Id = Guid.NewGuid(), CategoryId = catMains.Id, Category = catMains, NameAr = "وجبة عائلية", NameEn = "Family combo", Kind = MenuItemKinds.Combo, BasePrice = 8.000m, SortOrder = 3, IsActive = true };
        var compMain = new ComboComponent { Id = Guid.NewGuid(), ComboMenuItem = combo, SlotLabel = "Main", IsRequired = true, MinSelect = 1, MaxSelect = 2, SortOrder = 0 };
        compMain.Options.Add(ComboOption(compMain, itemBurger, 0m, true));
        compMain.Options.Add(ComboOption(compMain, itemChickenRice, 0m, false));
        compMain.Options.Add(ComboOption(compMain, itemShawarma, 0m, false));
        var compDrink = new ComboComponent { Id = Guid.NewGuid(), ComboMenuItem = combo, SlotLabel = "Drink", IsRequired = true, MinSelect = 1, MaxSelect = 1, SortOrder = 1 };
        compDrink.Options.Add(ComboOption(compDrink, itemCola, 0m, true));
        compDrink.Options.Add(ComboOption(compDrink, itemWater, 0m, false));
        combo.ComboComponents.Add(compMain);
        combo.ComboComponents.Add(compDrink);
        db.MenuItems.Add(combo);

        // --- Recipes: link items to ingredients (drives stock deduction) ---
        db.MenuItemRecipeLines.AddRange(
            Recipe(itemBurger, branch.Id, ingBeef, 0.150m),
            Recipe(itemBurger, branch.Id, ingBun, 1m),
            Recipe(itemBurger, branch.Id, ingCheese, 0.030m),
            Recipe(itemFries, branch.Id, ingPotato, 0.250m),
            Recipe(itemShawarma, branch.Id, ingChicken, 0.250m),
            Recipe(itemShawarma, branch.Id, ingBun, 1m),
            Recipe(itemChickenRice, branch.Id, ingChicken, 0.200m),
            Recipe(itemChickenRice, branch.Id, ingRice, 0.250m),
            Recipe(itemChickenRice, branch.Id, ingTomato, 0.080m),
            Recipe(itemCola, branch.Id, ingCola, 0.040m),
            Recipe(itemCola, branch.Id, ingIce, 0.120m),
            Recipe(itemWater, branch.Id, ingIce, 0.100m));

        // --- Warehouse + starting stock ---
        var warehouse = new Warehouse { Id = Guid.NewGuid(), BranchId = branch.Id, NameAr = "المخزن الرئيسي", NameEn = "Main warehouse", IsDefault = true, IsActive = true };
        db.Warehouses.Add(warehouse);
        db.WarehouseIngredientStocks.AddRange(
            Stock(warehouse.Id, ingBeef, 25m, 5m),
            Stock(warehouse.Id, ingBun, 120m, 20m),
            Stock(warehouse.Id, ingPotato, 60m, 10m),
            Stock(warehouse.Id, ingChicken, 90m, 15m),
            Stock(warehouse.Id, ingRice, 40m, 10m),
            Stock(warehouse.Id, ingTomato, 18m, 4m),
            Stock(warehouse.Id, ingCheese, 15m, 3m),
            Stock(warehouse.Id, ingCola, 8m, 2m),
            Stock(warehouse.Id, ingIce, 30m, 6m));

        // --- Demo tables / floor so the floor board is populated ---
        var floor = await db.RestaurantFloors.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.BranchId == branch.Id, cancellationToken);
        if (floor is null)
        {
            floor = new RestaurantFloor { Id = Guid.NewGuid(), BranchId = branch.Id, Name = "Main floor", SortOrder = 0, IsActive = true };
            db.RestaurantFloors.Add(floor);
        }
        if (!await db.RestaurantTables.IgnoreQueryFilters().AnyAsync(x => x.BranchId == branch.Id, cancellationToken))
        {
            db.RestaurantTables.AddRange(
                Table(branch.Id, floor.Id, "T01", 4, 20, 30, RestaurantTableShapes.Rectangle),
                Table(branch.Id, floor.Id, "T02", 4, 45, 28, RestaurantTableShapes.Rectangle),
                Table(branch.Id, floor.Id, "T03", 2, 70, 30, RestaurantTableShapes.Round),
                Table(branch.Id, floor.Id, "T04", 6, 25, 65, RestaurantTableShapes.Round),
                Table(branch.Id, floor.Id, "T05", 4, 55, 68, RestaurantTableShapes.Rectangle));
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static MenuCategory Category(string nameEn, string nameAr, int sortOrder) => new() { Id = Guid.NewGuid(), NameEn = nameEn, NameAr = nameAr, SortOrder = sortOrder, IsActive = true };
    private static MenuItem Item(MenuCategory category, string nameEn, string nameAr, decimal price, int sortOrder) => new() { Id = Guid.NewGuid(), CategoryId = category.Id, Category = category, NameEn = nameEn, NameAr = nameAr, Kind = MenuItemKinds.SingleProduct, BasePrice = price, SortOrder = sortOrder, IsActive = true };
    private static ModifierGroup Group(string nameEn, string nameAr, int min, int max, bool required) => new() { Id = Guid.NewGuid(), NameEn = nameEn, NameAr = nameAr, MinSelect = min, MaxSelect = max, IsRequired = required };
    private static ModifierOption Option(string nameEn, string nameAr, decimal delta) => new() { Id = Guid.NewGuid(), NameEn = nameEn, NameAr = nameAr, PriceDelta = delta, IsActive = true };
    private static void AttachItemModifiers(MenuItem item, params ModifierGroup[] groups)
    {
        foreach (var group in groups)
            item.ModifierGroups.Add(new MenuItemModifierGroup { MenuItemId = item.Id, MenuItem = item, ModifierGroupId = group.Id, ModifierGroup = group });
    }
    private static ComboComponentOption ComboOption(ComboComponent component, MenuItem item, decimal delta, bool isDefault) => new() { Id = Guid.NewGuid(), ComboComponentId = component.Id, ComboComponent = component, MenuItemId = item.Id, MenuItem = item, PriceDelta = delta, IsDefault = isDefault };
    private static Ingredient Ingredient(string nameEn, string nameAr, UnitOfMeasure unit) => new() { Id = Guid.NewGuid(), NameEn = nameEn, NameAr = nameAr, UnitOfMeasureId = unit.Id, UnitOfMeasure = unit };
    private static MenuItemRecipeLine Recipe(MenuItem item, Guid branchId, Ingredient ingredient, decimal quantity) => new() { Id = Guid.NewGuid(), MenuItemId = item.Id, MenuItem = item, BranchId = branchId, IngredientId = ingredient.Id, Ingredient = ingredient, QuantityRequired = quantity };
    private static WarehouseIngredientStock Stock(Guid warehouseId, Ingredient ingredient, decimal quantity, decimal threshold) => new() { WarehouseId = warehouseId, IngredientId = ingredient.Id, Ingredient = ingredient, CurrentQuantity = quantity, LowStockThreshold = threshold };
    private static RestaurantTable Table(Guid branchId, Guid floorId, string label, int capacity, int x, int y, string shape) => new() { Id = Guid.NewGuid(), BranchId = branchId, FloorId = floorId, Label = label, Capacity = capacity, PositionX = x, PositionY = y, Shape = shape, IsActive = true };
}
