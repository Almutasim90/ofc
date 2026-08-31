using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
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

var dataProtectionKeysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");
var dataProtectionBuilder = builder.Services.AddDataProtection();
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    // Without this, encrypted AI provider keys (Settings > AI) become unreadable
    // after every container restart/redeploy, since the default key ring lives on
    // the container's ephemeral filesystem. Point this at a mounted volume in prod.
    Directory.CreateDirectory(dataProtectionKeysPath);
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
}

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

static string? CleanEnvironmentValue(string name)
{
    var value = Environment.GetEnvironmentVariable(name)?.Trim();
    if (value?.Length >= 2 && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
        value = value[1..^1].Trim();
    return value;
}

var supabaseUrl = CleanEnvironmentValue("SUPABASE_URL")?.TrimEnd('/');
var supabaseSecretKey = CleanEnvironmentValue("SUPABASE_SECRET_KEY");
if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseSecretKey))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("SUPABASE_URL and SUPABASE_SECRET_KEY must be set outside Development.");
    }

    // Dev-only fallback so a fresh clone still boots without Storage configured;
    // image uploads will fail until real values are set in .env.
    supabaseUrl ??= "http://localhost";
    supabaseSecretKey ??= "unconfigured";
}

var storageOptions = new SupabaseStorageOptions(supabaseUrl, supabaseSecretKey);

builder.Services.AddInfrastructure(connectionString, jwtOptions, storageOptions);
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
builder.Services.AddRateLimiter(options=>options.AddFixedWindowLimiter("qr",x=>{x.PermitLimit=60;x.Window=TimeSpan.FromMinutes(1);x.QueueLimit=0;x.AutoReplenishment=true;}));

Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"));

// Same-origin in production (nginx serves the frontend and proxies /api on one
// domain), so this normally never applies there - it only matters if the
// frontend is ever split onto its own origin. FRONTEND_ORIGIN adds one extra
// allowed origin on top of the fixed local-dev list below.
var extraFrontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN");
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCorsPolicy, policy =>
    {
        List<string> origins =
        [
            "http://localhost:5173",
            "http://127.0.0.1:5173",
            "http://192.168.100.18:5173",
            "http://192.168.100.82:5173",
        ];
        if (!string.IsNullOrWhiteSpace(extraFrontendOrigin))
            origins.Add(extraFrontendOrigin);

        policy.WithOrigins(origins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Development always applies migrations/seed for a friction-free local setup.
// Outside Development this only runs when explicitly opted into (RUN_MIGRATIONS_ON_STARTUP=true) -
// required on first deploy to create the schema and bootstrap admin user; safe to
// leave on afterwards since both migrate and seed are idempotent.
if (app.Environment.IsDevelopment() || bool.TryParse(Environment.GetEnvironmentVariable("RUN_MIGRATIONS_ON_STARTUP"), out var runMigrations) && runMigrations)
{
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

// Terminate TLS at the reverse proxy (nginx) in production; trust its
// X-Forwarded-Proto so UseHttpsRedirection below doesn't redirect-loop on
// what looks like a plain-HTTP request arriving over the internal Docker network.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
