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
    /// ingredients, warehouse stock, recipes and tables) so the restaurant-catalog,
    /// inventory and stock-count screens are populated. Each record is resolved by
    /// name before being created, so it is idempotent and safe on repeat startups
    /// even when some data (e.g. a category) already exists.
    /// </summary>
    private static async Task SeedDemoRestaurantDataAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var branch = await db.Branches.IgnoreQueryFilters().OrderBy(x => x.NameEn).FirstOrDefaultAsync(cancellationToken);
        if (branch is null)
        {
            branch = new Branch { Id = Guid.NewGuid(), Code = "DEMO", NameAr = "الفرع الرئيسي", NameEn = "Main Branch", IsActive = true, NextOrderNumber = 1 };
            db.Branches.Add(branch);
        }

        // --- Units of measure (name is unique) ---
        var unitKg = await GetOrAddUnit(db, "Kilogram", "kg", true, cancellationToken);
        var unitL = await GetOrAddUnit(db, "Liter", "L", true, cancellationToken);
        var unitPc = await GetOrAddUnit(db, "Piece", "pc", true, cancellationToken);

        // --- Ingredients (resolved by English name) ---
        var ingBeef = await GetOrAddIngredient(db, "Beef", "لحم", unitKg, cancellationToken);
        var ingBun = await GetOrAddIngredient(db, "Burger bun", "خبز برجر", unitPc, cancellationToken);
        var ingPotato = await GetOrAddIngredient(db, "Potato", "بطاطس", unitKg, cancellationToken);
        var ingChicken = await GetOrAddIngredient(db, "Chicken", "دجاج", unitKg, cancellationToken);
        var ingRice = await GetOrAddIngredient(db, "Rice", "أرز", unitKg, cancellationToken);
        var ingTomato = await GetOrAddIngredient(db, "Tomato", "طماطم", unitKg, cancellationToken);
        var ingCheese = await GetOrAddIngredient(db, "Cheese", "جبن", unitKg, cancellationToken);
        var ingCola = await GetOrAddIngredient(db, "Cola syrup", "شراب كولا", unitL, cancellationToken);
        var ingIce = await GetOrAddIngredient(db, "Ice", "ثلج", unitKg, cancellationToken);

        // --- Menu categories (resolved by English name) ---
        var catStarters = await GetOrAddCategory(db, "Starters", "مقبلات", 0, cancellationToken);
        var catMains = await GetOrAddCategory(db, "Mains", "أطباق رئيسية", 1, cancellationToken);
        var catDrinks = await GetOrAddCategory(db, "Drinks", "مشروبات", 2, cancellationToken);
        var catDesserts = await GetOrAddCategory(db, "Desserts", "حلويات", 3, cancellationToken);

        // --- Menu items (resolved by English name within its category) ---
        var itemBurger = await GetOrAddMenuItem(db, catMains, "Beef burger", "برجر لحم", MenuItemKinds.SingleProduct, 3.500m, 0, cancellationToken);
        var itemFries = await GetOrAddMenuItem(db, catStarters, "French fries", "بطاطس مقلية", MenuItemKinds.SingleProduct, 1.200m, 1, cancellationToken);
        var itemShawarma = await GetOrAddMenuItem(db, catMains, "Chicken shawarma", "شاورما دجاج", MenuItemKinds.SingleProduct, 2.000m, 1, cancellationToken);
        var itemChickenRice = await GetOrAddMenuItem(db, catMains, "Chicken with rice", "دجاج مع أرز", MenuItemKinds.SingleProduct, 3.000m, 2, cancellationToken);
        var itemCola = await GetOrAddMenuItem(db, catDrinks, "Cola", "كولات", MenuItemKinds.SingleProduct, 0.500m, 0, cancellationToken);
        var itemWater = await GetOrAddMenuItem(db, catDrinks, "Water", "ماء", MenuItemKinds.SingleProduct, 0.300m, 1, cancellationToken);
        var itemIceCream = await GetOrAddMenuItem(db, catDesserts, "Ice cream", "آيس كريم", MenuItemKinds.SingleProduct, 1.500m, 0, cancellationToken);

        // --- Addons: modifier groups + options ---
        var grpSize = await GetOrAddModifierGroup(db, "Size", "الحجم", 1, 1, true, cancellationToken);
        EnsureOption(grpSize, "Small", "صغير", -0.500m);
        EnsureOption(grpSize, "Regular", "عادي", 0m);
        EnsureOption(grpSize, "Large", "كبير", 0.750m);

        var grpExtras = await GetOrAddModifierGroup(db, "Extras", "إضافات", 0, 3, false, cancellationToken);
        EnsureOption(grpExtras, "Extra cheese", "جبن إضافي", 0.400m);
        EnsureOption(grpExtras, "Extra fries", "بطاطس إضافية", 0.600m);
        EnsureOption(grpExtras, "Add cola", "أضف كولا", 0.500m);

        var grpSauce = await GetOrAddModifierGroup(db, "Sauce", "الصلصة", 0, 2, false, cancellationToken);
        EnsureOption(grpSauce, "Garlic sauce", "صلصة ثوم", 0.150m);
        EnsureOption(grpSauce, "Ketchup", "كاتشب", 0.100m);
        EnsureOption(grpSauce, "Tahini", "طحينة", 0.150m);

        var grpIce = await GetOrAddModifierGroup(db, "Ice level", "كمية الثلج", 1, 1, true, cancellationToken);
        EnsureOption(grpIce, "Normal", "عادي", 0m);
        EnsureOption(grpIce, "No ice", "بدون ثلج", 0m);

        // Wire addons to items (only when the link is missing)
        EnsureItemModifiers(itemBurger, grpSize, grpExtras);
        EnsureItemModifiers(itemShawarma, grpSize, grpSauce);
        EnsureItemModifiers(itemFries, grpSize, grpExtras);
        EnsureItemModifiers(itemCola, grpIce);
        EnsureItemModifiers(itemWater, grpIce);

        // --- Combo: a family combo composed of mains + drinks ---
        var combo = await GetOrAddMenuItem(db, catMains, "Family combo", "وجبة عائلية", MenuItemKinds.Combo, 8.000m, 3, cancellationToken);
        var compMain = EnsureComboComponent(combo, "Main", true, 1, 2, 0);
        EnsureComboOption(compMain, itemBurger, 0m, true);
        EnsureComboOption(compMain, itemChickenRice, 0m, false);
        EnsureComboOption(compMain, itemShawarma, 0m, false);
        var compDrink = EnsureComboComponent(combo, "Drink", true, 1, 1, 1);
        EnsureComboOption(compDrink, itemCola, 0m, true);
        EnsureComboOption(compDrink, itemWater, 0m, false);

        // --- Recipes: link items to ingredients (drives stock deduction) ---
        await EnsureRecipe(db, itemBurger, branch.Id, ingBeef, 0.150m, cancellationToken);
        await EnsureRecipe(db, itemBurger, branch.Id, ingBun, 1m, cancellationToken);
        await EnsureRecipe(db, itemBurger, branch.Id, ingCheese, 0.030m, cancellationToken);
        await EnsureRecipe(db, itemFries, branch.Id, ingPotato, 0.250m, cancellationToken);
        await EnsureRecipe(db, itemShawarma, branch.Id, ingChicken, 0.250m, cancellationToken);
        await EnsureRecipe(db, itemShawarma, branch.Id, ingBun, 1m, cancellationToken);
        await EnsureRecipe(db, itemChickenRice, branch.Id, ingChicken, 0.200m, cancellationToken);
        await EnsureRecipe(db, itemChickenRice, branch.Id, ingRice, 0.250m, cancellationToken);
        await EnsureRecipe(db, itemChickenRice, branch.Id, ingTomato, 0.080m, cancellationToken);
        await EnsureRecipe(db, itemCola, branch.Id, ingCola, 0.040m, cancellationToken);
        await EnsureRecipe(db, itemCola, branch.Id, ingIce, 0.120m, cancellationToken);
        await EnsureRecipe(db, itemWater, branch.Id, ingIce, 0.100m, cancellationToken);

        // --- Warehouse + starting stock (create warehouse if none exists on this branch) ---
        var warehouse = await db.Warehouses.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.BranchId == branch.Id, cancellationToken);
        if (warehouse is null)
        {
            warehouse = new Warehouse { Id = Guid.NewGuid(), BranchId = branch.Id, NameAr = "المخزن الرئيسي", NameEn = "Main warehouse", IsDefault = true, IsActive = true };
            db.Warehouses.Add(warehouse);
            await db.SaveChangesAsync(cancellationToken);
        }
        await EnsureStock(db, warehouse.Id, ingBeef, 25m, 5m, cancellationToken);
        await EnsureStock(db, warehouse.Id, ingBun, 120m, 20m, cancellationToken);
        await EnsureStock(db, warehouse.Id, ingPotato, 60m, 10m, cancellationToken);
        await EnsureStock(db, warehouse.Id, ingChicken, 90m, 15m, cancellationToken);
        await EnsureStock(db, warehouse.Id, ingRice, 40m, 10m, cancellationToken);
        await EnsureStock(db, warehouse.Id, ingTomato, 18m, 4m, cancellationToken);
        await EnsureStock(db, warehouse.Id, ingCheese, 15m, 3m, cancellationToken);
        await EnsureStock(db, warehouse.Id, ingCola, 8m, 2m, cancellationToken);
        await EnsureStock(db, warehouse.Id, ingIce, 30m, 6m, cancellationToken);

        // --- Demo floor + tables so the floor board is populated ---
        var floor = await db.RestaurantFloors.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.BranchId == branch.Id, cancellationToken);
        if (floor is null)
        {
            floor = new RestaurantFloor { Id = Guid.NewGuid(), BranchId = branch.Id, Name = "Main floor", SortOrder = 0, IsActive = true };
            db.RestaurantFloors.Add(floor);
            await db.SaveChangesAsync(cancellationToken);
        }
        await EnsureTable(db, branch.Id, floor.Id, "T01", 4, 20, 30, RestaurantTableShapes.Rectangle, cancellationToken);
        await EnsureTable(db, branch.Id, floor.Id, "T02", 4, 45, 28, RestaurantTableShapes.Rectangle, cancellationToken);
        await EnsureTable(db, branch.Id, floor.Id, "T03", 2, 70, 30, RestaurantTableShapes.Round, cancellationToken);
        await EnsureTable(db, branch.Id, floor.Id, "T04", 6, 25, 65, RestaurantTableShapes.Round, cancellationToken);
        await EnsureTable(db, branch.Id, floor.Id, "T05", 4, 55, 68, RestaurantTableShapes.Rectangle, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<UnitOfMeasure> GetOrAddUnit(AppDbContext db, string name, string symbol, bool isBase, CancellationToken ct)
    {
        var unit = await db.UnitsOfMeasure.FirstOrDefaultAsync(x => x.Name == name, ct);
        if (unit is null) { unit = new UnitOfMeasure { Id = Guid.NewGuid(), Name = name, Symbol = symbol, IsBase = isBase }; db.UnitsOfMeasure.Add(unit); }
        return unit;
    }

    private static async Task<Ingredient> GetOrAddIngredient(AppDbContext db, string nameEn, string nameAr, UnitOfMeasure unit, CancellationToken ct)
    {
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(x => x.NameEn == nameEn, ct);
        if (ingredient is null) { ingredient = new Ingredient { Id = Guid.NewGuid(), NameEn = nameEn, NameAr = nameAr, UnitOfMeasureId = unit.Id, UnitOfMeasure = unit }; db.Ingredients.Add(ingredient); }
        return ingredient;
    }

    private static async Task<MenuCategory> GetOrAddCategory(AppDbContext db, string nameEn, string nameAr, int sortOrder, CancellationToken ct)
    {
        var category = await db.MenuCategories.FirstOrDefaultAsync(x => x.NameEn == nameEn, ct);
        if (category is null) { category = new MenuCategory { Id = Guid.NewGuid(), NameEn = nameEn, NameAr = nameAr, SortOrder = sortOrder, IsActive = true }; db.MenuCategories.Add(category); }
        return category;
    }

    private static async Task<MenuItem> GetOrAddMenuItem(AppDbContext db, MenuCategory category, string nameEn, string nameAr, string kind, decimal price, int sortOrder, CancellationToken ct)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(x => x.NameEn == nameEn, ct);
        if (item is null) { item = new MenuItem { Id = Guid.NewGuid(), CategoryId = category.Id, Category = category, NameEn = nameEn, NameAr = nameAr, Kind = kind, BasePrice = price, SortOrder = sortOrder, IsActive = true }; db.MenuItems.Add(item); }
        return item;
    }

    private static async Task<ModifierGroup> GetOrAddModifierGroup(AppDbContext db, string nameEn, string nameAr, int min, int max, bool required, CancellationToken ct)
    {
        var group = await db.ModifierGroups.FirstOrDefaultAsync(x => x.NameEn == nameEn, ct);
        if (group is null) { group = new ModifierGroup { Id = Guid.NewGuid(), NameEn = nameEn, NameAr = nameAr, MinSelect = min, MaxSelect = max, IsRequired = required }; db.ModifierGroups.Add(group); }
        return group;
    }

    private static void EnsureOption(ModifierGroup group, string nameEn, string nameAr, decimal delta)
    {
        if (group.Options.Any(x => x.NameEn == nameEn)) return;
        group.Options.Add(new ModifierOption { Id = Guid.NewGuid(), ModifierGroupId = group.Id, ModifierGroup = group, NameEn = nameEn, NameAr = nameAr, PriceDelta = delta, IsActive = true });
    }

    private static void EnsureItemModifiers(MenuItem item, params ModifierGroup[] groups)
    {
        foreach (var group in groups)
            if (!item.ModifierGroups.Any(x => x.ModifierGroupId == group.Id))
                item.ModifierGroups.Add(new MenuItemModifierGroup { MenuItemId = item.Id, MenuItem = item, ModifierGroupId = group.Id, ModifierGroup = group });
    }

    private static ComboComponent EnsureComboComponent(MenuItem combo, string slotLabel, bool isRequired, int min, int max, int sortOrder)
    {
        var component = combo.ComboComponents.FirstOrDefault(x => x.SlotLabel == slotLabel);
        if (component is null)
        {
            component = new ComboComponent { Id = Guid.NewGuid(), ComboMenuItemId = combo.Id, ComboMenuItem = combo, SlotLabel = slotLabel, IsRequired = isRequired, MinSelect = min, MaxSelect = max, SortOrder = sortOrder };
            combo.ComboComponents.Add(component);
        }
        return component;
    }

    private static void EnsureComboOption(ComboComponent component, MenuItem item, decimal delta, bool isDefault)
    {
        if (component.Options.Any(x => x.MenuItemId == item.Id)) return;
        component.Options.Add(new ComboComponentOption { Id = Guid.NewGuid(), ComboComponentId = component.Id, ComboComponent = component, MenuItemId = item.Id, MenuItem = item, PriceDelta = delta, IsDefault = isDefault });
    }

    private static async Task EnsureRecipe(AppDbContext db, MenuItem item, Guid branchId, Ingredient ingredient, decimal quantity, CancellationToken ct)
    {
        var exists = await db.MenuItemRecipeLines.IgnoreQueryFilters().AnyAsync(x => x.MenuItemId == item.Id && x.BranchId == branchId && x.IngredientId == ingredient.Id, ct);
        if (!exists) db.MenuItemRecipeLines.Add(new MenuItemRecipeLine { Id = Guid.NewGuid(), MenuItemId = item.Id, MenuItem = item, BranchId = branchId, IngredientId = ingredient.Id, Ingredient = ingredient, QuantityRequired = quantity });
    }

    private static async Task EnsureStock(AppDbContext db, Guid warehouseId, Ingredient ingredient, decimal quantity, decimal threshold, CancellationToken ct)
    {
        var exists = await db.WarehouseIngredientStocks.IgnoreQueryFilters().AnyAsync(x => x.WarehouseId == warehouseId && x.IngredientId == ingredient.Id, ct);
        if (!exists) db.WarehouseIngredientStocks.Add(new WarehouseIngredientStock { WarehouseId = warehouseId, IngredientId = ingredient.Id, Ingredient = ingredient, CurrentQuantity = quantity, LowStockThreshold = threshold });
    }

    private static async Task EnsureTable(AppDbContext db, Guid branchId, Guid floorId, string label, int capacity, int x, int y, string shape, CancellationToken ct)
    {
        var exists = await db.RestaurantTables.IgnoreQueryFilters().AnyAsync(t => t.BranchId == branchId && t.Label == label, ct);
        if (!exists) db.RestaurantTables.Add(new RestaurantTable { Id = Guid.NewGuid(), BranchId = branchId, FloorId = floorId, Label = label, Capacity = capacity, PositionX = x, PositionY = y, Shape = shape, IsActive = true });
    }
}
