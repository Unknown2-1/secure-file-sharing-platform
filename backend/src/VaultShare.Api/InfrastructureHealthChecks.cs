using System.Net.Sockets;
using System.Security.Cryptography;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VaultShare.Application.Encryption;
using VaultShare.Application.Storage;
using VaultShare.Infrastructure.Persistence;

internal sealed class DatabaseHealthCheck(VaultShareDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken) =>
        await dbContext.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
}

internal sealed class ObjectStorageHealthCheck(IObjectStorage objectStorage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken) =>
        await objectStorage.HealthCheckAsync(cancellationToken)
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy();
}

internal sealed class EncryptionProviderHealthCheck(IKeyEncryptionProvider provider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        try
        {
            var wrapped = await provider.WrapAsync(key, cancellationToken);
            var unwrapped = await provider.UnwrapAsync(wrapped, cancellationToken);
            try { return CryptographicOperations.FixedTimeEquals(key, unwrapped) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy(); }
            finally { CryptographicOperations.ZeroMemory(unwrapped); }
        }
        catch (CryptographicException) { return HealthCheckResult.Unhealthy(); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}

internal sealed class ClamAvHealthCheck(IConfiguration configuration, IHostEnvironment environment) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing")) return HealthCheckResult.Healthy();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(configuration["CLAMAV_HOST"]!, int.Parse(configuration["CLAMAV_PORT"]!), timeout.Token);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy();
        }
    }
}
