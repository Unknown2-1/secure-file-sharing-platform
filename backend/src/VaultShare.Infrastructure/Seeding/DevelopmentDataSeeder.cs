using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VaultShare.Application.Encryption;
using VaultShare.Application.Shares;
using VaultShare.Application.Storage;
using VaultShare.Domain.Auditing;
using VaultShare.Domain.Files;
using VaultShare.Domain.Notifications;
using VaultShare.Domain.Shares;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.Infrastructure.Seeding;

public sealed class DevelopmentDataSeeder(
    VaultShareDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IFileEncryptionService encryptionService,
    IObjectStorage objectStorage,
    ISecureTokenGenerator tokenGenerator,
    IPasswordHasher<Share> passwordHasher)
{
    private const string DemoPassword = "ChangeMe123!";
    private static readonly Guid PersonalWorkspaceId = Guid.Parse("019f6b70-0000-7000-8000-000000000001");
    private static readonly Guid TeamAlphaId = Guid.Parse("019f6b70-0000-7000-8000-000000000002");
    private static readonly Guid TeamBetaId = Guid.Parse("019f6b70-0000-7000-8000-000000000003");
    private static readonly Guid DemoFileId = Guid.Parse("019f6b70-0000-7000-8000-000000000010");
    private static readonly Guid DemoMetadataId = Guid.Parse("019f6b70-0000-7000-8000-000000000011");

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var users = new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);
        foreach (var email in new[]
        {
            "owner@example.com", "admin@example.com", "admin2@example.com",
            "member@example.com", "member2@example.com", "member3@example.com", "member4@example.com",
            "viewer@example.com", "viewer2@example.com",
        })
        {
            users[email] = await EnsureUserAsync(email, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        await EnsureWorkspaceAsync(PersonalWorkspaceId, "Ruang Pribadi Demo", users["owner@example.com"],
            [(users["owner@example.com"], WorkspaceRole.Owner)], now, cancellationToken);
        await EnsureWorkspaceAsync(TeamAlphaId, "Tim Arunika", users["owner@example.com"],
            [
                (users["owner@example.com"], WorkspaceRole.Owner),
                (users["admin@example.com"], WorkspaceRole.Admin),
                (users["member@example.com"], WorkspaceRole.Member),
                (users["member2@example.com"], WorkspaceRole.Member),
                (users["viewer@example.com"], WorkspaceRole.Viewer),
            ], now, cancellationToken);
        await EnsureWorkspaceAsync(TeamBetaId, "Tim Cakrawala", users["owner@example.com"],
            [
                (users["owner@example.com"], WorkspaceRole.Owner),
                (users["admin2@example.com"], WorkspaceRole.Admin),
                (users["member3@example.com"], WorkspaceRole.Member),
                (users["member4@example.com"], WorkspaceRole.Member),
                (users["viewer2@example.com"], WorkspaceRole.Viewer),
            ], now, cancellationToken);

        await EnsureEncryptedFixtureAsync(users["owner@example.com"], now, cancellationToken);
        await EnsureSharesAsync(users["owner@example.com"], now, cancellationToken);

        if (!await dbContext.Notifications.AnyAsync(item => item.UserId == users["owner@example.com"].Id &&
            item.Type == "DemoReady", cancellationToken))
        {
            dbContext.Notifications.Add(new Notification(Guid.CreateVersion7(), users["owner@example.com"].Id,
                "DemoReady", "Data demo siap", "Workspace, file terenkripsi, dan contoh share telah dibuat.", false, now));
            dbContext.AuditEvents.Add(new AuditEvent(Guid.CreateVersion7(), TeamAlphaId,
                users["owner@example.com"].Id, "DevelopmentDataSeeded", "Workspace", TeamAlphaId.ToString("D"),
                now, "development", "VaultShare.Seeder", "development-seed", "Success", "{}"));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ApplicationUser> EnsureUserAsync(string email, CancellationToken cancellationToken)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null) return existing;
        var user = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = email.Split('@')[0],
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var result = await userManager.CreateAsync(user, DemoPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Demo user creation failed: {string.Join(',', result.Errors.Select(error => error.Code))}");
        cancellationToken.ThrowIfCancellationRequested();
        return user;
    }

    private async Task EnsureWorkspaceAsync(Guid id, string name, ApplicationUser owner,
        IReadOnlyList<(ApplicationUser User, WorkspaceRole Role)> memberships, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Workspaces.AnyAsync(item => item.Id == id, cancellationToken))
            dbContext.Workspaces.Add(new Workspace(id, name, owner.Id, now));
        foreach (var membership in memberships)
        {
            if (!await dbContext.WorkspaceMembers.AnyAsync(item => item.WorkspaceId == id &&
                item.UserId == membership.User.Id, cancellationToken))
                dbContext.WorkspaceMembers.Add(new WorkspaceMember(id, membership.User.Id, membership.Role, now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureEncryptedFixtureAsync(ApplicationUser owner, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await dbContext.StoredFiles.AnyAsync(item => item.Id == DemoFileId, cancellationToken)) return;
        var content = Encoding.UTF8.GetBytes("VaultShare demo fixture. Konten ini dienkripsi sebelum masuk object storage.\n");
        const string objectKey = "encrypted/019f6b70-0000-7000-8000-000000000002/019f6b70-0000-7000-8000-000000000010";
        await objectStorage.DeleteAsync(objectKey, cancellationToken);
        await using var plaintext = new MemoryStream(content, writable: false);
        await using var ciphertext = new MemoryStream();
        var encrypted = await encryptionService.EncryptAsync(DemoFileId, plaintext, ciphertext, 64 * 1024, cancellationToken);
        ciphertext.Position = 0;
        await objectStorage.PutAsync(objectKey, ciphertext, ciphertext.Length,
            new Dictionary<string, string> { ["vaultshare-file-id"] = DemoFileId.ToString("D") }, cancellationToken);

        var metadata = new FileEncryptionMetadata(DemoMetadataId, encrypted.Algorithm, encrypted.AlgorithmVersion,
            encrypted.WrappedDataKey.Ciphertext, encrypted.WrappedDataKey.Nonce,
            encrypted.WrappedDataKey.AuthenticationTag, encrypted.WrappedDataKey.KeyProvider,
            encrypted.WrappedDataKey.KeyIdentifier, encrypted.ChunkSize, encrypted.BaseNonce, encrypted.CreatedAt);
        var file = new StoredFile(DemoFileId, TeamAlphaId, owner.Id, "panduan-demo.txt", "panduan-demo.txt",
            objectKey, content.LongLength, "text/plain", now);
        file.MarkUploaded();
        file.MarkValidated("text/plain", Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());
        file.MarkScanning();
        file.MarkScanClean();
        file.MarkEncryptionStarted();
        file.MarkEncrypted(metadata.Id);
        file.MarkAvailable(now);
        dbContext.FileEncryptionMetadata.Add(metadata);
        dbContext.StoredFiles.Add(file);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSharesAsync(ApplicationUser owner, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Shares.AnyAsync(item => item.WorkspaceId == TeamAlphaId, cancellationToken)) return;
        var definitions = new[]
        {
            ("Share aktif", now.AddDays(7), (int?)10, false, false, false),
            ("Share kedaluwarsa", now.AddDays(-1), (int?)5, false, false, false),
            ("Share dicabut", now.AddDays(7), (int?)5, false, true, false),
            ("Share sekali unduh", now.AddDays(1), (int?)1, true, false, false),
            ("Share berpassword", now.AddDays(3), (int?)3, false, false, true),
        };
        foreach (var (name, expiresAt, maximum, oneTime, revoked, protectedByPassword) in definitions)
        {
            var createdAt = expiresAt <= now ? expiresAt.AddDays(-7) : now;
            var token = tokenGenerator.Generate(32);
            var share = new Share(Guid.CreateVersion7(), TeamAlphaId, owner.Id, tokenGenerator.Generate(16),
                tokenGenerator.Hash(token), tokenGenerator.Hash($"seed-{name}"), name,
                "Contoh konfigurasi share untuk lingkungan demo.", null, expiresAt, maximum, oneTime, true, createdAt);
            if (protectedByPassword) share.SetPasswordHash(passwordHasher.HashPassword(share, "DemoShare123!"));
            if (revoked) share.Revoke(owner.Id, now);
            dbContext.Shares.Add(share);
            dbContext.ShareItems.Add(new ShareItem(share.Id, DemoFileId));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
