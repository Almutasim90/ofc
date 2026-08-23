using Microsoft.Extensions.DependencyInjection;
using POS.Application.Auth;
using POS.Application.Users;

namespace POS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        return services;
    }
}
