using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using VaultShare.Infrastructure;
using VaultShare.Infrastructure.Persistence;
using VaultShare.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 8 * 1024 * 1024;
    options.Limits.MaxRequestBufferSize = 64 * 1024;
    options.Limits.MaxRequestHeaderCount = 100;
    options.Limits.MaxRequestHeadersTotalSize = 32 * 1024;
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddSerilog(configuration => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());
ProductionConfigurationGuard.Validate(builder.Configuration, builder.Environment);

var allowedOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"])
    .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: ["ready"])
    .AddCheck<ClamAvHealthCheck>("clamav", tags: ["ready"])
    .AddCheck<EncryptionProviderHealthCheck>("encryption-key-provider", tags: ["ready"]);
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST", "PATCH", "DELETE", "OPTIONS")
    .WithHeaders("Content-Type", "X-CSRF-TOKEN", "Idempotency-Key", "Upload-Offset")
    .WithExposedHeaders("Upload-Offset")
    .AllowCredentials()));
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = builder.Environment.IsProduction()
        ? "__Host-VaultShare.Antiforgery"
        : "VaultShare.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    RateLimitingPolicies.AddIpPolicy(options, "register", 5);
    RateLimitingPolicies.AddIpPolicy(options, "login", 10);
    RateLimitingPolicies.AddIpPolicy(options, "account-recovery", 5);
    RateLimitingPolicies.AddIpPolicy(options, "workspace-invitation", 10);
    RateLimitingPolicies.AddIpPolicy(options, "upload-create", 20);
    RateLimitingPolicies.AddIpPolicy(options, "share-create", 20);
    RateLimitingPolicies.AddIpPolicy(options, "public-share-access", 10);
    RateLimitingPolicies.AddIpPolicy(options, "public-download", 30);
});
builder.Services.AddVaultShareInfrastructure(builder.Configuration, builder.Environment);

var app = builder.Build();

if (args.Contains("--migrate-only", StringComparer.Ordinal) || app.Environment.IsDevelopment())
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await migrationScope.ServiceProvider.GetRequiredService<VaultShareDbContext>().Database.MigrateAsync();
    if (app.Environment.IsDevelopment() &&
        bool.TryParse(app.Configuration["SEED_DEMO_DATA"], out var seedDemoData) && seedDemoData)
        await migrationScope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(CancellationToken.None);
    if (args.Contains("--migrate-only", StringComparer.Ordinal)) return;
}

if (app.Environment.IsProduction()) app.UseHsts();
app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers["X-Correlation-ID"].ToString();
    context.TraceIdentifier = supplied.Length is >= 8 and <= 64 && supplied.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
        ? supplied
        : Guid.CreateVersion7().ToString("N");
    context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
    using var correlation = LogContext.PushProperty("CorrelationId", context.TraceIdentifier);
    try
    {
        await next(context);
    }
    finally
    {
        try
        {
            await AuditRequestRecorder.RecordAsync(context, app.Services, app.Configuration, app.Environment, CancellationToken.None);
        }
        catch (Exception exception)
        {
            app.Logger.LogError(exception, "Audit request recording failed for correlation {CorrelationId}", context.TraceIdentifier);
        }
    }
});

app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] =
        "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
    context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
    context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-site";
    if (!app.Environment.IsDevelopment())
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; base-uri 'none'; frame-ancestors 'none'";
    context.Response.Headers.CacheControl = "no-store";
    await next(context);
});

app.UseHttpsRedirection();

app.UseCors("frontend");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new()
{
    Predicate = _ => false,
});
app.MapHealthChecks("/health/ready", new()
{
    Predicate = registration => registration.Tags.Contains("ready"),
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<VaultShareDbContext>().Database.EnsureCreatedAsync();
}

app.Run();

internal static class ProductionConfigurationGuard
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction()) return;

        RequireSafeSecret(configuration, "FILE_ENCRYPTION_KEK", requireBase64Key: true);
        RequireSafeSecret(configuration, "PRIVACY_IP_HASH_KEY", requireBase64Key: true);
        RequireSafeSecret(configuration, "DATABASE_CONNECTION_STRING");
        RequireSafeSecret(configuration, "OBJECT_STORAGE_SECRET_KEY");
        RequireHttpsUrl(configuration, "PUBLIC_APP_URL");
        RequireHttpsUrl(configuration, "FRONTEND_URL");

        var origins = configuration["CORS_ALLOWED_ORIGINS"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (origins.Length == 0 || origins.Any(origin => origin.Contains('*') || !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Production CORS_ALLOWED_ORIGINS must contain only explicit HTTPS origins.");
        if (!bool.TryParse(configuration["CLAMAV_FAIL_CLOSED"], out var failClosed) || !failClosed)
            throw new InvalidOperationException("CLAMAV_FAIL_CLOSED must be true in production.");
        if (bool.TryParse(configuration["SEED_DEMO_DATA"], out var seedDemoData) && seedDemoData)
            throw new InvalidOperationException("SEED_DEMO_DATA cannot be enabled in production.");
    }

    private static void RequireSafeSecret(IConfiguration configuration, string key, bool requireBase64Key = false)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.Contains("REPLACE", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("local-only", StringComparison.OrdinalIgnoreCase) || value.Contains("development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{key} must be replaced before production startup.");
        if (!requireBase64Key) return;
        try
        {
            if (Convert.FromBase64String(value).Length != 32) throw new FormatException();
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{key} must be a Base64-encoded 32-byte key.");
        }
    }

    private static void RequireHttpsUrl(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.IsLoopback)
            throw new InvalidOperationException($"{key} must be a non-local HTTPS URL in production.");
    }
}

public partial class Program;

internal static class RateLimitingPolicies
{
    public static void AddIpPolicy(
        Microsoft.AspNetCore.RateLimiting.RateLimiterOptions options,
        string name,
        int permitLimit)
    {
        options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    }
}
