using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using POS.API.Authorization;
using POS.API.Middleware;
using POS.Application;
using POS.Infrastructure;
using POS.Infrastructure.Persistence;
using POS.Infrastructure.Persistence.Seed;
using POS.Infrastructure.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDataProtection();

const string FrontendCorsPolicy = "FrontendDev";

if (builder.Environment.IsDevelopment())
{
    var envFilePath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", ".env");
    if (File.Exists(envFilePath))
    {
        Env.Load(envFilePath);
    }
}

var connectionString = Environment.GetEnvironmentVariable("SUPABASE_DB_CONNECTION")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("JWT_SECRET must be set outside Development.");
    }

    // Dev-only fallback so a fresh clone runs without extra setup; tokens won't
    // survive a restart. Set JWT_SECRET in .env for a stable dev secret.
    jwtSecret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));
}

var jwtOptions = new JwtOptions(
    Secret: jwtSecret,
    Issuer: Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "POS.API",
    Audience: Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "POS.Client",
    ExpiryMinutes: 480);

builder.Services.AddInfrastructure(connectionString, jwtOptions);
builder.Services.AddApplication();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));

builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordHasher = scope.ServiceProvider.GetRequiredService<POS.Application.Abstractions.IPasswordHasher>();
    try
    {
        db.Database.Migrate();
        await SeedData.SeedAsync(db, passwordHasher);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Database migrate/seed skipped - database is not reachable yet.");
    }
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
