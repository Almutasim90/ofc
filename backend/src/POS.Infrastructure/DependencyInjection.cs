using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS.Application.Abstractions;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Services;

namespace POS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string? connectionString, JwtOptions jwtOptions)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IPasswordHasher, PasswordHasherService>();
        services.AddSingleton(jwtOptions);
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.AddHostedService<AutomaticShiftClosingService>();

        services.AddDbContext<AppDbContext>((sp, options) =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        return services;
    }
}
