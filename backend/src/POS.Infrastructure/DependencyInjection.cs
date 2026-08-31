using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Services;

namespace POS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string? connectionString, JwtOptions jwtOptions, SupabaseStorageOptions storageOptions)
    {
        services.AddHttpContextAccessor();
        services.AddHttpClient();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddSingleton(jwtOptions);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton(storageOptions);
        services.AddSingleton<IFileStorageService, SupabaseStorageService>();
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.AddScoped<IEmailNotificationSender, DatabaseEmailNotificationSender>();
        services.AddScoped<IRawPrinterClient, TcpRawPrinterClient>();
        services.AddHostedService<AutomaticShiftClosingService>();
        services.AddHostedService<LowStockMonitoringService>();

        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        return services;
    }
}
