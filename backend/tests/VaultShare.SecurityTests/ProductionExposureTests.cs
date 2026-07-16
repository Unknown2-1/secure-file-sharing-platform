using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VaultShare.SecurityTests;

public sealed class ProductionExposureTests
{
    [Fact]
    public async Task Production_disables_swagger_and_emits_hsts_over_https()
    {
        using var factory = CreateProductionFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://vaultshare.example"),
            AllowAutoRedirect = false,
        });

        using var swagger = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.NotFound, swagger.StatusCode);
        Assert.True(swagger.Headers.Contains("Strict-Transport-Security"));
        Assert.DoesNotContain("Swagger UI", await swagger.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Production_rejects_demo_seed_before_serving_requests()
    {
        using var factory = CreateProductionFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("SEED_DEMO_DATA", "true"));
        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("SEED_DEMO_DATA", exception.ToString(), StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateProductionFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("FILE_ENCRYPTION_KEK", "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=");
            builder.UseSetting("PRIVACY_IP_HASH_KEY", "RkVEQ0JBOTg3NjU0MzIxMEZFRENCQTk4NzY1NDMyMTA=");
            builder.UseSetting("FILE_ENCRYPTION_KEY_ID", "production-test-kek");
            builder.UseSetting("DATABASE_CONNECTION_STRING", "Host=database;Database=vaultshare;Username=vaultshare;Password=StrongProductionTest!42");
            builder.UseSetting("OBJECT_STORAGE_ENDPOINT", "storage:9000");
            builder.UseSetting("OBJECT_STORAGE_ACCESS_KEY", "production-test-access");
            builder.UseSetting("OBJECT_STORAGE_SECRET_KEY", "StrongObjectStorageTest!42");
            builder.UseSetting("OBJECT_STORAGE_BUCKET", "vaultshare-test");
            builder.UseSetting("CLAMAV_HOST", "clamav");
            builder.UseSetting("CLAMAV_PORT", "3310");
            builder.UseSetting("CLAMAV_FAIL_CLOSED", "true");
            builder.UseSetting("PUBLIC_APP_URL", "https://vaultshare.example");
            builder.UseSetting("FRONTEND_URL", "https://vaultshare.example");
            builder.UseSetting("CORS_ALLOWED_ORIGINS", "https://vaultshare.example");
            builder.UseSetting("SEED_DEMO_DATA", "false");
        });
}
