using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Application.Encryption;
using VaultShare.Application.Maintenance;
using VaultShare.Application.Storage;
using VaultShare.Infrastructure.Persistence;
using VaultShare.Infrastructure.Seeding;

namespace VaultShare.IntegrationTests;

public sealed class DevelopmentSeedTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public DevelopmentSeedTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Seeder_is_idempotent_and_fixture_exists_only_as_authenticated_ciphertext()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>();
        await seeder.SeedAsync(default);
        await seeder.SeedAsync(default);

        var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        Assert.Equal(9, await db.Users.CountAsync(user => user.Email != null && user.Email.EndsWith("@example.com")));
        Assert.Equal(3, await db.Workspaces.CountAsync(workspace => workspace.Name.Contains("Demo") ||
            workspace.Name == "Tim Arunika" || workspace.Name == "Tim Cakrawala"));
        Assert.Equal(5, await db.Shares.CountAsync(share => share.WorkspaceId ==
            Guid.Parse("019f6b70-0000-7000-8000-000000000002")));

        var file = await db.StoredFiles.AsNoTracking().SingleAsync(item => item.Id ==
            Guid.Parse("019f6b70-0000-7000-8000-000000000010"));
        var metadata = await db.FileEncryptionMetadata.AsNoTracking().SingleAsync(item => item.Id == file.EncryptionMetadataId);
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await using var encrypted = await storage.GetStreamAsync(file.StoredObjectKey, default);
        await using var copy = new MemoryStream();
        await encrypted.CopyToAsync(copy);
        var plaintextText = "VaultShare demo fixture. Konten ini dienkripsi sebelum masuk object storage.\n";
        Assert.DoesNotContain(Convert.ToHexString(Encoding.UTF8.GetBytes(plaintextText)),
            Convert.ToHexString(copy.ToArray()), StringComparison.Ordinal);

        copy.Position = 0;
        await using var decrypted = new MemoryStream();
        await scope.ServiceProvider.GetRequiredService<IFileEncryptionService>().DecryptAsync(file.Id, copy, decrypted,
            new FileEncryptionResult(metadata.Algorithm, metadata.AlgorithmVersion, metadata.ChunkSize,
                metadata.BaseNonce, new WrappedDataKey(metadata.WrappedDataKey, metadata.KeyWrapNonce,
                    metadata.KeyWrapAuthenticationTag, metadata.KeyProvider, metadata.KeyIdentifier, metadata.CreatedAt),
                metadata.CreatedAt), default);
        Assert.Equal(plaintextText, Encoding.UTF8.GetString(decrypted.ToArray()));

        var consistency = scope.ServiceProvider.GetRequiredService<IStorageConsistencyService>();
        Assert.Empty((await consistency.CheckMissingObjectsAsync(100, default)).MissingFileIds);
        var orphanKey = $"encrypted/{Guid.NewGuid():D}/{Guid.NewGuid():N}";
        await using (var orphanContent = new MemoryStream(copy.ToArray(), writable: false))
            await storage.PutAsync(orphanKey, orphanContent, orphanContent.Length, null, default);
        var dryRun = await consistency.ReconcileAsync(100, false, default);
        Assert.Equal(1, dryRun.OrphanObjects);
        Assert.Equal(0, dryRun.OrphanObjectsDeleted);
        Assert.True(await storage.ExistsAsync(orphanKey, default));
        var cleanup = await consistency.ReconcileAsync(100, true, default);
        Assert.Equal(1, cleanup.OrphanObjectsDeleted);
        Assert.False(await storage.ExistsAsync(orphanKey, default));
        await storage.DeleteAsync(file.StoredObjectKey, default);
        Assert.Contains(file.Id, (await consistency.CheckMissingObjectsAsync(100, default)).MissingFileIds);
    }
}
