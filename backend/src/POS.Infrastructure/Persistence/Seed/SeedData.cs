using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Domain.Constants;
using POS.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

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
            db.SalesChannels.Add(new SalesChannel { Id = SalesChannelIds.InStore, NameAr = "المحل", NameEn = "In-store", IsActive = true, IsInStore = true });
            await db.SaveChangesAsync(cancellationToken);
        }

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

        await SeedDemoCatalogAndAugustSalesAsync(db, cancellationToken);
    }

    private static async Task SeedDemoCatalogAndAugustSalesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var catalog = new[]
        {
            new ProductSeed("بكورة (5 حبات)", "Bakora (5 pieces)", "Food", 0.100m, "https://lolat-db.almutasim.site/storage/v1/object/public/uploads/products/d8b0f49a46094bdcba8c311a60168751.webp"),
            new ProductSeed("بطاطا (4 حبات)", "Potato fritters (4 pieces)", "Food", 0.100m, "https://lolat-db.almutasim.site/storage/v1/object/public/uploads/products/ad5c8c1040ae47518cfbc2ed5f12c521.webp"),
            new ProductSeed("شاي أخضر", "Green tea", "Tea", 0.100m, "https://lolat-db.almutasim.site/storage/v1/object/public/uploads/products/fc32003b89164f91b692488f16670003.webp"),
            new ProductSeed("شاي كرك صغير", "Small karak tea", "Tea", 0.100m, "https://lolat-db.almutasim.site/storage/v1/object/public/uploads/products/f29fa08c86a040a88bd21588e4beaa17.webp"),
            new ProductSeed("شاي كرك كبير", "Large karak tea", "Tea", 0.200m, "https://lolat-db.almutasim.site/storage/v1/object/public/uploads/products/13b7d4da25b04d55a07d66e559a6e9a8.webp"),
            new ProductSeed("زلابية (5 حبات)", "Zalabia (5 pieces)", "Sweet", 0.100m, "https://lolat-db.almutasim.site/storage/v1/object/public/uploads/products/a39376eba7074c7dadb3c5c0b019963e.webp"),
            new ProductSeed("دونات (حبتان)", "Donuts (2 pieces)", "Sweet", 0.100m, "https://lolat-db.almutasim.site/storage/v1/object/public/uploads/products/136d8a5b7270468eafc3019f721e4535.webp"),
            new ProductSeed("لولاة (12 حبة)", "Lolat (12 pieces)", "Food", 0.100m, "https://lolat-db.almutasim.site/storage/v1/object/public/uploads/products/3730418980a94446a39e0b4f0097a7ad.webp"),
            new ProductSeed("كينزا كولا", "Kinza cola", "Drinks", 0.300m, "https://lolat-db.almutasim.site/storage/v1/object/public/uploads/products/b9bb6dcd73fe4d1ea008b12d7f7ceaf2.webp"),
            new ProductSeed("مياه صغيرة", "Small water", "Drinks", 0.100m, "https://lolat-db.almutasim.site/storage/v1/object/public/uploads/products/e958ef257fea4dd9aa93d9a84f6d6dc2.webp"),
        };

        var existingNames = await db.Products.Select(p => p.NameEn).ToListAsync(cancellationToken);
        foreach (var item in catalog.Where(item => !existingNames.Contains(item.NameEn)))
        {
            db.Products.Add(new Product
            {
                Id = DeterministicGuid($"demo-product:{item.NameEn}"), NameAr = item.NameAr, NameEn = item.NameEn,
                Category = item.Category, Price = item.Price, IconOrImageUrl = item.ImageUrl, IsActive = true,
            });
        }
        var legacyLolat = await db.Products.FirstOrDefaultAsync(p => p.NameEn == "LOLAT", cancellationToken);
        if (legacyLolat is not null) legacyLolat.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);

        var products = await db.Products.Where(p => catalog.Select(c => c.NameEn).Contains(p.NameEn)).ToListAsync(cancellationToken);
        var branches = await db.Branches.Where(b => b.IsActive).ToListAsync(cancellationToken);
        var cashier = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Username == BootstrapAdminUsername, cancellationToken);
        if (cashier is null || branches.Count == 0 || products.Count == 0) return;

        var start = new DateOnly(2026, 8, 1);
        var end = new DateOnly(2026, 8, 24);
        var invoiceWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["khd"] = 48, // Al Khoudh is intentionally the strongest demo branch.
            ["khr"] = 37, // Al Khuwair is second.
            ["bid"] = 24,
            ["brk"] = 20,
            ["khb"] = 17,
        };
        var expectedShiftIds = branches.SelectMany(branch => Enumerable.Range(0, end.DayNumber - start.DayNumber + 1)
            .Select(offset => DeterministicGuid($"demo-shift:{branch.Id}:{start.AddDays(offset):yyyy-MM-dd}"))).ToList();
        var khoudhForIds = branches.FirstOrDefault(b => b.Code.Equals("khd", StringComparison.OrdinalIgnoreCase));
        if (khoudhForIds is not null)
            expectedShiftIds.AddRange(Enumerable.Range(0, end.DayNumber - start.DayNumber + 1)
                .Select(offset => DeterministicGuid($"demo-khoudh-campaign:{khoudhForIds.Id}:{start.AddDays(offset):yyyy-MM-dd}")));
        expectedShiftIds.AddRange(branches.SelectMany(branch => Enumerable.Range(0, end.DayNumber - start.DayNumber + 1)
            .Select(offset => DeterministicGuid($"demo-signature-products:{branch.Id}:{start.AddDays(offset):yyyy-MM-dd}"))));
        var existingShiftIds = (await db.Shifts.IgnoreQueryFilters()
            .Where(s => expectedShiftIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet();

        foreach (var branch in branches)
        {
            var dailyBase = invoiceWeights.GetValueOrDefault(branch.Code, 14);
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var shiftId = DeterministicGuid($"demo-shift:{branch.Id}:{date:yyyy-MM-dd}");
                if (existingShiftIds.Contains(shiftId)) continue;

                var random = new Random(HashCode.Combine(branch.Id, date.DayNumber));
                var openedAt = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(7, 0)), DateTimeKind.Utc);
                var shift = new Shift
                {
                    Id = shiftId, BranchId = branch.Id, CashierUserId = cashier.Id, OpeningCash = 20m,
                    OpenedAt = openedAt, ClosedAt = openedAt.AddHours(15), Status = ShiftStatus.Closed,
                };
                var invoiceCount = dailyBase + random.Next(-3, 5);
                for (var invoice = 0; invoice < invoiceCount; invoice++)
                {
                    var createdAt = openedAt.AddMinutes(random.Next(15, 14 * 60));
                    var sale = new Sale
                    {
                        Id = DeterministicGuid($"demo-sale:{branch.Id}:{date:yyyy-MM-dd}:{invoice}"),
                        BranchId = branch.Id, ChannelId = SalesChannelIds.InStore, ShiftId = shift.Id, CashierUserId = cashier.Id, BusinessDate = date,
                        CreatedAt = createdAt, PaymentMethod = random.NextDouble() < 0.62 ? "Cash" : "Card",
                        Status = SaleStatus.Completed,
                    };
                    var lineCount = random.Next(1, 4);
                    foreach (var product in products.OrderBy(_ => random.Next()).Take(lineCount))
                    {
                        var quantity = random.Next(1, product.NameEn.Contains("karak", StringComparison.OrdinalIgnoreCase) ? 5 : 3);
                        var lineTotal = product.Price * quantity;
                        sale.Items.Add(new SaleItem
                        {
                            Id = Guid.NewGuid(), ProductId = product.Id, ProductNameSnapshot = product.NameAr,
                            UnitPriceSnapshot = product.Price, Quantity = quantity, LineTotal = lineTotal,
                        });
                        sale.TotalAmount += lineTotal;
                    }
                    shift.Sales.Add(sale);
                }

                shift.ClosingCashExpected = 20m + shift.Sales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount);
                shift.ClosingCashActual = shift.ClosingCashExpected;
                shift.VarianceAmount = 0m;
                db.Shifts.Add(shift);
            }
        }

        // A small, deterministic Al Khoudh-only campaign guarantees that both invoice count
        // and revenue rank Al Khoudh first, while Al Khuwair remains second in demo analytics.
        var khoudh = branches.FirstOrDefault(b => b.Code.Equals("khd", StringComparison.OrdinalIgnoreCase));
        if (khoudh is not null)
        {
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var shiftId = DeterministicGuid($"demo-khoudh-campaign:{khoudh.Id}:{date:yyyy-MM-dd}");
                if (existingShiftIds.Contains(shiftId)) continue;
                var openedAt = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(16, 0)), DateTimeKind.Utc);
                var shift = new Shift
                {
                    Id = shiftId, BranchId = khoudh.Id, CashierUserId = cashier.Id, OpeningCash = 10m,
                    OpenedAt = openedAt, ClosedAt = openedAt.AddHours(6), Status = ShiftStatus.Closed,
                };
                for (var invoice = 0; invoice < 12; invoice++)
                {
                    var product = products[invoice % products.Count];
                    var quantity = product.Price >= 0.200m ? 4 : 3;
                    var sale = new Sale
                    {
                        Id = DeterministicGuid($"demo-khoudh-campaign-sale:{khoudh.Id}:{date:yyyy-MM-dd}:{invoice}"),
                        BranchId = khoudh.Id, ChannelId = SalesChannelIds.InStore, ShiftId = shift.Id, CashierUserId = cashier.Id, BusinessDate = date,
                        CreatedAt = openedAt.AddMinutes(invoice * 24), PaymentMethod = invoice % 3 == 0 ? "Card" : "Cash",
                        Status = SaleStatus.Completed, TotalAmount = product.Price * quantity,
                    };
                    sale.Items.Add(new SaleItem
                    {
                        Id = Guid.NewGuid(), ProductId = product.Id, ProductNameSnapshot = product.NameAr,
                        UnitPriceSnapshot = product.Price, Quantity = quantity, LineTotal = sale.TotalAmount,
                    });
                    shift.Sales.Add(sale);
                }
                shift.ClosingCashExpected = 10m + shift.Sales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount);
                shift.ClosingCashActual = shift.ClosingCashExpected;
                shift.VarianceAmount = 0m;
                db.Shifts.Add(shift);
            }
        }

        var lolat = products.FirstOrDefault(p => p.NameEn == "Lolat (12 pieces)");
        var kinza = products.FirstOrDefault(p => p.NameEn == "Kinza cola");
        var water = products.FirstOrDefault(p => p.NameEn == "Small water");
        if (lolat is not null && kinza is not null && water is not null)
        {
            var signatureWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                { ["khd"] = 10, ["khr"] = 8, ["bid"] = 5, ["brk"] = 4, ["khb"] = 3 };
            foreach (var branch in branches)
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                var shiftId = DeterministicGuid($"demo-signature-products:{branch.Id}:{date:yyyy-MM-dd}");
                if (existingShiftIds.Contains(shiftId)) continue;
                var openedAt = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(5, 30)), DateTimeKind.Utc);
                var shift = new Shift { Id = shiftId, BranchId = branch.Id, CashierUserId = cashier.Id,
                    OpeningCash = 10m, OpenedAt = openedAt, ClosedAt = openedAt.AddHours(4), Status = ShiftStatus.Closed };
                var count = signatureWeights.GetValueOrDefault(branch.Code, 3);
                for (var invoice = 0; invoice < count; invoice++)
                {
                    var drink = invoice % 2 == 0 ? kinza : water;
                    var sale = new Sale { Id = DeterministicGuid($"demo-signature-sale:{branch.Id}:{date:yyyy-MM-dd}:{invoice}"),
                        BranchId = branch.Id, ChannelId = SalesChannelIds.InStore, ShiftId = shiftId, CashierUserId = cashier.Id, BusinessDate = date,
                        CreatedAt = openedAt.AddMinutes(invoice * 18), PaymentMethod = invoice % 3 == 0 ? "Card" : "Cash",
                        Status = SaleStatus.Completed };
                    foreach (var (product, quantity) in new[] { (lolat, 4m), (drink, 1m) })
                    {
                        var total = product.Price * quantity;
                        sale.Items.Add(new SaleItem { Id = Guid.NewGuid(), ProductId = product.Id,
                            ProductNameSnapshot = product.NameAr, UnitPriceSnapshot = product.Price,
                            Quantity = quantity, LineTotal = total });
                        sale.TotalAmount += total;
                    }
                    shift.Sales.Add(sale);
                }
                shift.ClosingCashExpected = 10m + shift.Sales.Where(s => s.PaymentMethod == "Cash").Sum(s => s.TotalAmount);
                shift.ClosingCashActual = shift.ClosingCashExpected; shift.VarianceAmount = 0m;
                db.Shifts.Add(shift);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private sealed record ProductSeed(string NameAr, string NameEn, string Category, decimal Price, string ImageUrl);
}
