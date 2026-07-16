using Serilog;
using VaultShare.Infrastructure;
using VaultShare.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog(configuration => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());
builder.Services.AddVaultShareInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
if (args.Contains("rewrap-keys", StringComparer.Ordinal))
{
    return await KeyRotationCommand.RunAsync(host.Services, builder.Configuration, args, CancellationToken.None);
}
if (args.Contains("storage-check", StringComparer.Ordinal))
{
    await using var scope = host.Services.CreateAsyncScope();
    var result = await scope.ServiceProvider
        .GetRequiredService<VaultShare.Application.Maintenance.IStorageConsistencyService>()
        .ReconcileAsync(10_000, args.Contains("--delete-orphans", StringComparer.Ordinal), CancellationToken.None);
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
    return result.MissingFileIds.Count == 0 ? 0 : 2;
}

await host.RunAsync();
return 0;
