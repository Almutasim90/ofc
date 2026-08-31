using Microsoft.Extensions.DependencyInjection;
using POS.Application.Auth;
using POS.Application.Catalog;
using POS.Application.Closing;
using POS.Application.Inventory;
using POS.Application.Reports;
using POS.Application.Sales;
using POS.Application.Shifts;
using POS.Application.Users;
using POS.Application.Channels;
using POS.Application.Notifications;
using POS.Application.AI;
using POS.Application.Settings;
using POS.Application.RestaurantCatalog;
using POS.Application.Modifiers;
using POS.Application.Orders;
using POS.Application.RestaurantInventory;
using POS.Application.Printing;
using POS.Application.Payments;
using POS.Application.CashShifts;

namespace POS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<ChannelService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<AiInsightService>();
        services.AddScoped<EmailSettingsService>();
        services.AddScoped<ReceiptSettingsService>();
        services.AddHttpClient();
        services.AddScoped<BranchService>();
        services.AddScoped<RestaurantCatalogService>();
        services.AddScoped<ModifierService>();
        services.AddScoped<RestaurantOrderService>();
        services.AddScoped<OrderCancellationService>();
        services.AddScoped<RestaurantInventoryService>();
        services.AddScoped<StockCountService>();
        services.AddScoped<PrinterAdminService>();
        services.AddScoped<OrderPrintingService>();
        services.AddScoped<OrderPaymentService>();
        services.AddScoped<CashShiftService>();
        services.AddScoped<ProductService>();
        services.AddScoped<RawMaterialService>();
        services.AddScoped<RecipeService>();
        services.AddScoped<StockService>();
        services.AddScoped<SaleService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<VoidService>();
        services.AddScoped<ClosingScheduleService>();
        services.AddScoped<ReportService>();
        return services;
    }
}
