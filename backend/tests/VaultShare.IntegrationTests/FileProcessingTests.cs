using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VaultShare.Application.Encryption;
using VaultShare.Application.Notifications;
using VaultShare.Application.Scanning;
using VaultShare.Application.Storage;
using VaultShare.Application.Uploads;
using VaultShare.Domain.Workspaces;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.IntegrationTests;

public sealed class FileProcessingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FileProcessingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Clean_upload_is_inspected_scanned_encrypted_stored_and_plaintext_removed()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAndLoginAsync(client, _factory);
        var original = "VaultShare processing pipeline"u8.ToArray();
        var uploadId = await UploadAndFinalizeAsync(client, workspaceId, "sample.txt", "text/plain", original);

        await using var scope = _factory.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<ICompletedUploadProcessor>();
        Assert.Equal(1, await processor.ProcessBatchAsync(1, default));
        var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        var upload = await db.FileUploads.AsNoTracking().Include(item => item.StoredFile).SingleAsync(item => item.Id == uploadId);
        var file = upload.StoredFile;
        Assert.Equal("Completed", upload.Status.ToString());
        Assert.Equal("Available", file.AvailabilityStatus.ToString());
        Assert.Equal("Clean", file.MalwareScanStatus.ToString());
        Assert.Equal("Encrypted", file.EncryptionStatus.ToString());
        Assert.False(File.Exists(upload.TemporaryPath));
        var notification = await db.Notifications.SingleAsync(item => item.UserId == file.OwnerUserId && item.Type == "FileAvailable");
        Assert.Null(notification.EmailSentAt);
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<INotificationDeliveryService>().DeliverPendingEmailAsync(10, default));
        Assert.NotNull(notification.EmailSentAt);

        var metadata = await db.FileEncryptionMetadata.AsNoTracking().SingleAsync(item => item.Id == file.EncryptionMetadataId);
        var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await using var cipherStream = await storage.GetStreamAsync(file.StoredObjectKey, default);
        await using var cipherCopy = new MemoryStream();
        await cipherStream.CopyToAsync(cipherCopy);
        Assert.DoesNotContain(Convert.ToHexString(original), Convert.ToHexString(cipherCopy.ToArray()), StringComparison.Ordinal);
        cipherCopy.Position = 0;
        await using var decrypted = new MemoryStream();
        var encryption = scope.ServiceProvider.GetRequiredService<IFileEncryptionService>();
        await encryption.DecryptAsync(file.Id, cipherCopy, decrypted, new FileEncryptionResult(
            metadata.Algorithm, metadata.AlgorithmVersion, metadata.ChunkSize, metadata.BaseNonce,
            new WrappedDataKey(metadata.WrappedDataKey, metadata.KeyWrapNonce, metadata.KeyWrapAuthenticationTag,
                metadata.KeyProvider, metadata.KeyIdentifier, metadata.CreatedAt), metadata.CreatedAt), default);
        Assert.Equal(original, decrypted.ToArray());

        using var detail = await client.GetAsync($"/api/v1/files/{file.Id:D}");
        var safeResponse = await detail.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.DoesNotContain("storedObjectKey", safeResponse, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wrappedDataKey", safeResponse, StringComparison.OrdinalIgnoreCase);
        using var notifications = await client.GetAsync("/api/v1/notifications");
        Assert.Contains("File siap digunakan", await notifications.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var dashboard = await client.GetAsync($"/api/v1/dashboard?workspaceId={workspaceId:D}");
        Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        var dashboardBody = await dashboard.Content.ReadAsStringAsync();
        Assert.Contains("\"totalFiles\":1", dashboardBody, StringComparison.Ordinal);
        Assert.Contains("\"uploadsLastSevenDays\":1", dashboardBody, StringComparison.Ordinal);
        using var export = await client.GetAsync("/api/v1/users/me/export");
        var exportBody = await export.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Contains("sample.txt", exportBody, StringComparison.Ordinal);
        Assert.DoesNotContain("storedObjectKey", exportBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wrappedDataKey", exportBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secretToken", exportBody, StringComparison.OrdinalIgnoreCase);

        using var deleted = await SendJsonAsync(client, HttpMethod.Delete, $"/api/v1/files/{file.Id:D}", new { }, "file-delete-key");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        using var restored = await SendJsonAsync(client, HttpMethod.Post, $"/api/v1/files/{file.Id:D}/restore", new { }, "file-restore-key");
        Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
    }

    [Fact]
    public async Task Mime_spoofing_fails_closed_and_creates_no_object()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterAndLoginAsync(client, _factory);
        var uploadId = await UploadAndFinalizeAsync(client, workspaceId, "spoof.png", "image/png", "not png"u8.ToArray());

        await using var scope = _factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ICompletedUploadProcessor>().ProcessBatchAsync(1, default));
        var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        var upload = await db.FileUploads.AsNoTracking().Include(item => item.StoredFile).SingleAsync(item => item.Id == uploadId);
        Assert.Equal("Failed", upload.Status.ToString());
        Assert.Equal("Failed", upload.StoredFile.AvailabilityStatus.ToString());
        Assert.False(await scope.ServiceProvider.GetRequiredService<IObjectStorage>().ExistsAsync(upload.StoredFile.StoredObjectKey, default));
    }

    [Theory]
    [InlineData(MalwareScanOutcome.Infected, "Infected", "Quarantined")]
    [InlineData(MalwareScanOutcome.ScannerUnavailable, "ScannerUnavailable", "Failed")]
    [InlineData(MalwareScanOutcome.Error, "Failed", "Failed")]
    public async Task Malware_or_scanner_failure_never_becomes_available(
        MalwareScanOutcome outcome,
        string scanStatus,
        string availability)
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMalwareScanner>();
            services.AddSingleton<IMalwareScanner>(new ResultScanner(outcome));
        }));
        using var client = factory.CreateClient();
        var workspaceId = await RegisterAndLoginAsync(client, factory);
        var uploadId = await UploadAndFinalizeAsync(client, workspaceId, "scan.txt", "text/plain", "safe fixture"u8.ToArray());

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ICompletedUploadProcessor>().ProcessBatchAsync(1, default));
        var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        var upload = await db.FileUploads.AsNoTracking().Include(item => item.StoredFile).SingleAsync(item => item.Id == uploadId);
        Assert.Equal("Failed", upload.Status.ToString());
        Assert.Equal(scanStatus, upload.StoredFile.MalwareScanStatus.ToString());
        Assert.Equal(availability, upload.StoredFile.AvailabilityStatus.ToString());
        Assert.False(File.Exists(upload.TemporaryPath));
        Assert.False(await scope.ServiceProvider.GetRequiredService<IObjectStorage>().ExistsAsync(upload.StoredFile.StoredObjectKey, default));
    }

    [Fact]
    public async Task Purge_after_grace_removes_ciphertext_and_wrapped_key()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder
            .UseSetting("DELETED_FILE_GRACE_PERIOD", "00:00:00"));
        using var client = factory.CreateClient();
        var workspaceId = await RegisterAndLoginAsync(client, factory);
        var uploadId = await UploadAndFinalizeAsync(client, workspaceId, "purge.txt", "text/plain", "purge me"u8.ToArray());
        Guid fileId;
        string objectKey;
        Guid metadataId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICompletedUploadProcessor>().ProcessBatchAsync(1, default);
            var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
            var upload = await db.FileUploads.AsNoTracking().Include(item => item.StoredFile).SingleAsync(item => item.Id == uploadId);
            fileId = upload.StoredFile.Id;
            objectKey = upload.StoredFile.StoredObjectKey;
            metadataId = Assert.IsType<Guid>(upload.StoredFile.EncryptionMetadataId);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await SendJsonAsync(client, HttpMethod.Delete, $"/api/v1/files/{fileId:D}", new { }, "purge-delete-key")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendJsonAsync(client, HttpMethod.Delete, $"/api/v1/files/{fileId:D}/purge", new { }, "purge-execute-key")).StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        var purged = await verifyDb.StoredFiles.AsNoTracking().SingleAsync(item => item.Id == fileId);
        Assert.Equal("Purged", purged.AvailabilityStatus.ToString());
        Assert.Null(purged.EncryptionMetadataId);
        Assert.False(await verifyDb.FileEncryptionMetadata.AnyAsync(item => item.Id == metadataId));
        Assert.False(await verifyScope.ServiceProvider.GetRequiredService<IObjectStorage>().ExistsAsync(objectKey, default));
    }

    [Fact]
    public async Task Internal_grants_enforce_permission_revocation_and_workspace_boundary()
    {
        using var owner = _factory.CreateClient();
        using var member = _factory.CreateClient();
        using var viewer = _factory.CreateClient();
        using var outsider = _factory.CreateClient();
        var workspaceId = await RegisterAndLoginAsync(owner, _factory);
        var memberEmail = await RegisterUserAsync(member, "internal-member");
        var viewerEmail = await RegisterUserAsync(viewer, "internal-viewer");
        await RegisterUserAsync(outsider, "internal-outsider");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
            var memberUser = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(memberEmail));
            var viewerUser = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(viewerEmail));
            db.WorkspaceMembers.AddRange(
                new WorkspaceMember(workspaceId, memberUser.Id, WorkspaceRole.Member, DateTimeOffset.UtcNow),
                new WorkspaceMember(workspaceId, viewerUser.Id, WorkspaceRole.Viewer, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var original = "Internal access remains workspace-scoped"u8.ToArray();
        var uploadId = await UploadAndFinalizeAsync(owner, workspaceId, "internal.txt", "text/plain", original);
        Guid fileId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ICompletedUploadProcessor>()
                .ProcessBatchAsync(1, default));
            var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
            fileId = await db.FileUploads.Where(item => item.Id == uploadId)
                .Select(item => item.StoredFileId).SingleAsync();
        }

        using var memberGrantResponse = await SendJsonAsync(owner, HttpMethod.Post,
            $"/api/v1/files/{fileId:D}/internal-grants",
            new { recipientEmail = memberEmail, permission = "Download", expiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        Assert.Equal(HttpStatusCode.Created, memberGrantResponse.StatusCode);
        var memberGrant = Assert.IsType<InternalGrantResponse>(
            await memberGrantResponse.Content.ReadFromJsonAsync<InternalGrantResponse>());

        using var viewerGrantResponse = await SendJsonAsync(owner, HttpMethod.Post,
            $"/api/v1/files/{fileId:D}/internal-grants",
            new { recipientEmail = viewerEmail, permission = "View", expiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        Assert.Equal(HttpStatusCode.Created, viewerGrantResponse.StatusCode);

        using var memberList = await member.GetAsync($"/api/v1/files?workspaceId={workspaceId:D}");
        Assert.Equal(HttpStatusCode.OK, memberList.StatusCode);
        Assert.Contains("internal.txt", await memberList.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        using var memberDownload = await member.GetAsync($"/api/v1/internal-files/{fileId:D}/download");
        Assert.Equal(HttpStatusCode.OK, memberDownload.StatusCode);
        Assert.Equal(original, await memberDownload.Content.ReadAsByteArrayAsync());

        using var viewerDetail = await viewer.GetAsync($"/api/v1/files/{fileId:D}");
        Assert.Equal(HttpStatusCode.OK, viewerDetail.StatusCode);
        using var viewerPreview = await viewer.GetAsync($"/api/v1/internal-files/{fileId:D}/preview");
        Assert.Equal(HttpStatusCode.OK, viewerPreview.StatusCode);
        Assert.Equal(original, await viewerPreview.Content.ReadAsByteArrayAsync());
        using var viewerDownload = await viewer.GetAsync($"/api/v1/internal-files/{fileId:D}/download");
        Assert.Equal(HttpStatusCode.Forbidden, viewerDownload.StatusCode);
        using var outsiderDetail = await outsider.GetAsync($"/api/v1/files/{fileId:D}");
        Assert.Equal(HttpStatusCode.Forbidden, outsiderDetail.StatusCode);

        using var grants = await owner.GetAsync($"/api/v1/files/{fileId:D}/internal-grants");
        var grantsBody = await grants.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, grants.StatusCode);
        Assert.Contains(memberEmail, grantsBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", grantsBody, StringComparison.OrdinalIgnoreCase);

        using var revoke = await SendJsonAsync(owner, HttpMethod.Delete,
            $"/api/v1/internal-file-grants/{memberGrant.Id:D}", new { });
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var revokedDownload = await member.GetAsync($"/api/v1/internal-files/{fileId:D}/download");
        Assert.Equal(HttpStatusCode.Forbidden, revokedDownload.StatusCode);
    }

    private async Task<Guid> UploadAndFinalizeAsync(HttpClient client, Guid workspaceId, string filename, string mime, byte[] content)
    {
        using var create = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/uploads", new { workspaceId, filename, fileSize = content.Length, clientMimeType = mime }, $"create-{Guid.NewGuid():N}");
        var upload = Assert.IsType<UploadResponse>(await create.Content.ReadFromJsonAsync<UploadResponse>());
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/uploads/{upload.Id:D}") { Content = new ByteArrayContent(content) };
        patch.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        patch.Headers.Add("Upload-Offset", "0");
        patch.Headers.Add("X-CSRF-TOKEN", csrf!.RequestToken);
        using var patched = await client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode);
        using var finalized = await SendJsonAsync(client, HttpMethod.Post, $"/api/v1/uploads/{upload.Id:D}/finalize", new { }, $"finalize-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
        return upload.Id;
    }

    private static async Task<Guid> RegisterAndLoginAsync(HttpClient client, WebApplicationFactory<Program> factory)
    {
        var email = $"processing-{Guid.NewGuid():N}@example.com";
        using var register = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/auth/register", new { email, password = "ValidPass123!", displayName = "Processing Test" });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            Assert.True((await users.ConfirmEmailAsync(user, await users.GenerateEmailConfirmationTokenAsync(user))).Succeeded);
        }
        using var login = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/auth/login", new { email, password = "ValidPass123!" });
        var workspaces = await client.GetFromJsonAsync<WorkspaceResponse[]>("/api/v1/workspaces");
        return Assert.Single(workspaces!).Id;
    }

    private async Task<string> RegisterUserAsync(HttpClient client, string prefix)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@example.com";
        using var register = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/auth/register",
            new { email, password = "ValidPass123!", displayName = "Internal Access Test" });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            Assert.True((await users.ConfirmEmailAsync(user,
                await users.GenerateEmailConfirmationTokenAsync(user))).Succeeded);
        }

        using var login = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/auth/login",
            new { email, password = "ValidPass123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return email;
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(HttpClient client, HttpMethod method, string path, object body, string? idempotencyKey = null)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.RequestToken);
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string RequestToken);
    private sealed record WorkspaceResponse(Guid Id);
    private sealed record UploadResponse(Guid Id);
    private sealed record InternalGrantResponse(Guid Id);

    private sealed class ResultScanner(MalwareScanOutcome outcome) : IMalwareScanner
    {
        public Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken) =>
            Task.FromResult(new MalwareScanResult(outcome));
    }
}
