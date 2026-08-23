using Microsoft.Extensions.DependencyInjection;
using POS.Application.Auth;
using POS.Application.Catalog;
using POS.Application.Inventory;
using POS.Application.Sales;
using POS.Application.Shifts;
using POS.Application.Users;

namespace POS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<BranchService>();
        services.AddScoped<ProductService>();
        services.AddScoped<RawMaterialService>();
        services.AddScoped<RecipeService>();
        services.AddScoped<StockService>();
        services.AddScoped<SaleService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<VoidService>();
        return services;
    }
}
