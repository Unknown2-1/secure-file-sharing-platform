using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Application.Uploads;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.IntegrationTests;

public sealed class SecureShareDownloadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public SecureShareDownloadTests(WebApplicationFactory<Program> factory) =>
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

    [Fact]
    public async Task Password_share_uses_hashes_enforces_limit_streams_original_and_revokes()
    {
        using var owner = _factory.CreateClient();
        var (workspaceId, fileId, original) = await PrepareAvailableFileAsync(owner, "shared.txt", "share payload"u8.ToArray());
        var idempotencyKey = $"share-{Guid.NewGuid():N}";
        using var createdResponse = await SendJsonAsync(owner, HttpMethod.Post, "/api/v1/shares", new
        {
            workspaceId,
            fileIds = new[] { fileId },
            name = "Dokumen aman",
            description = "Deskripsi <script>alert(1)</script>",
            password = "SeparatePass123!",
            startsAt = (DateTimeOffset?)null,
            expiresAt = DateTimeOffset.UtcNow.AddDays(1),
            maximumDownloads = 2,
            isOneTime = false,
            allowPreview = true,
        }, idempotencyKey);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = Assert.IsType<ShareCreatedResponse>(await createdResponse.Content.ReadFromJsonAsync<ShareCreatedResponse>());

        using var duplicate = await SendJsonAsync(owner, HttpMethod.Post, "/api/v1/shares", new
        {
            workspaceId,
            fileIds = new[] { fileId },
            name = "Duplicate",
            password = (string?)null,
            startsAt = (DateTimeOffset?)null,
            expiresAt = DateTimeOffset.UtcNow.AddDays(1),
            maximumDownloads = 2,
            isOneTime = false,
            allowPreview = false,
        }, idempotencyKey);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
            var stored = await db.Shares.AsNoTracking().SingleAsync(item => item.Id == created.Id);
            Assert.NotEqual(created.SecretToken, stored.SecretTokenHash);
            Assert.DoesNotContain(created.SecretToken, stored.SecretTokenHash, StringComparison.Ordinal);
            Assert.NotEqual("SeparatePass123!", stored.PasswordHash);
            Assert.Equal(1, await db.Shares.CountAsync(item => item.CreatedByUserId == stored.CreatedByUserId && item.CreationIdempotencyKeyHash == stored.CreationIdempotencyKeyHash));
            var audit = await db.AuditEvents.AsNoTracking().SingleAsync(item => item.Action == "ShareChanged" && item.TargetId == created.Id.ToString("D"));
            Assert.Equal(workspaceId, audit.WorkspaceId);
            Assert.DoesNotContain(created.SecretToken, audit.SafeMetadataJson, StringComparison.Ordinal);
            Assert.DoesNotContain("SeparatePass123!", audit.SafeMetadataJson, StringComparison.Ordinal);
            Assert.DoesNotContain(stored.SecretTokenHash, audit.SafeMetadataJson, StringComparison.Ordinal);
        }

        using var publicClient = _factory.CreateClient();
        using var wrong = await SendJsonAsync(publicClient, HttpMethod.Post, "/api/v1/public/shares/access", new
        { created.PublicIdentifier, created.SecretToken, password = "WrongPass123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        using var access = await SendJsonAsync(publicClient, HttpMethod.Post, "/api/v1/public/shares/access", new
        { created.PublicIdentifier, created.SecretToken, password = "SeparatePass123!" });
        Assert.Equal(HttpStatusCode.OK, access.StatusCode);

        Assert.Equal(original, await DownloadAsync(publicClient, fileId, HttpStatusCode.OK));
        Assert.Equal(original, await DownloadAsync(publicClient, fileId, HttpStatusCode.OK));
        _ = await DownloadAsync(publicClient, fileId, HttpStatusCode.Forbidden);

        using var revoke = await SendJsonAsync(owner, HttpMethod.Post, $"/api/v1/shares/{created.Id:D}/revoke", new { }, "share-revoke-key");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        using var afterRevoke = await SendJsonAsync(publicClient, HttpMethod.Post, "/api/v1/public/shares/access", new
        { created.PublicIdentifier, created.SecretToken, password = "SeparatePass123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, afterRevoke.StatusCode);
    }

    [Fact]
    public async Task One_time_share_allows_only_one_concurrent_download()
    {
        using var owner = _factory.CreateClient();
        var (workspaceId, fileId, original) = await PrepareAvailableFileAsync(owner, "once.txt", "one time"u8.ToArray());
        using var createdResponse = await SendJsonAsync(owner, HttpMethod.Post, "/api/v1/shares", new
        {
            workspaceId,
            fileIds = new[] { fileId },
            name = "Sekali unduh",
            password = (string?)null,
            startsAt = (DateTimeOffset?)null,
            expiresAt = DateTimeOffset.UtcNow.AddDays(1),
            maximumDownloads = (int?)null,
            isOneTime = true,
            allowPreview = true,
        }, $"one-time-{Guid.NewGuid():N}");
        var created = Assert.IsType<ShareCreatedResponse>(await createdResponse.Content.ReadFromJsonAsync<ShareCreatedResponse>());
        using var publicClient = _factory.CreateClient();
        using var access = await SendJsonAsync(publicClient, HttpMethod.Post, "/api/v1/public/shares/access", new
        { created.PublicIdentifier, created.SecretToken, password = (string?)null });
        Assert.Equal(HttpStatusCode.OK, access.StatusCode);

        using var preview = await publicClient.GetAsync($"/api/v1/previews/{fileId:D}");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal("text/plain", preview.Content.Headers.ContentType?.MediaType);
        Assert.Equal(original, await preview.Content.ReadAsByteArrayAsync());

        var first = CreateDownloadRequest(fileId);
        var second = CreateDownloadRequest(fileId);
        var responses = await Task.WhenAll(publicClient.SendAsync(first), publicClient.SendAsync(second));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Forbidden);
            var success = responses.Single(response => response.StatusCode == HttpStatusCode.OK);
            Assert.Equal(original, await success.Content.ReadAsByteArrayAsync());
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
            first.Dispose();
            second.Dispose();
        }
    }

    private async Task<(Guid WorkspaceId, Guid FileId, byte[] Content)> PrepareAvailableFileAsync(HttpClient client, string filename, byte[] content)
    {
        var email = $"share-{Guid.NewGuid():N}@example.com";
        _ = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/auth/register", new { email, password = "ValidPass123!", displayName = "Share Owner" });
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            Assert.True((await users.ConfirmEmailAsync(user, await users.GenerateEmailConfirmationTokenAsync(user))).Succeeded);
        }
        _ = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/auth/login", new { email, password = "ValidPass123!" });
        var workspaceId = Assert.Single((await client.GetFromJsonAsync<WorkspaceResponse[]>("/api/v1/workspaces"))!).Id;
        using var create = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/uploads", new
        { workspaceId, filename, fileSize = content.Length, clientMimeType = "text/plain" }, $"upload-{Guid.NewGuid():N}");
        var uploadId = (await create.Content.ReadFromJsonAsync<UploadResponse>())!.Id;
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        var patch = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/uploads/{uploadId:D}") { Content = new ByteArrayContent(content) };
        patch.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        patch.Headers.Add("Upload-Offset", "0"); patch.Headers.Add("X-CSRF-TOKEN", csrf!.RequestToken);
        using var patched = await client.SendAsync(patch);
        _ = await SendJsonAsync(client, HttpMethod.Post, $"/api/v1/uploads/{uploadId:D}/finalize", new { }, $"finalize-{Guid.NewGuid():N}");
        await using var processingScope = _factory.Services.CreateAsyncScope();
        await processingScope.ServiceProvider.GetRequiredService<ICompletedUploadProcessor>().ProcessBatchAsync(1, default);
        var db = processingScope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        var fileId = await db.FileUploads.Where(item => item.Id == uploadId).Select(item => item.StoredFileId).SingleAsync();
        return (workspaceId, fileId, content);
    }

    private static async Task<byte[]> DownloadAsync(HttpClient client, Guid fileId, HttpStatusCode status)
    {
        using var response = await client.SendAsync(CreateDownloadRequest(fileId));
        Assert.Equal(status, response.StatusCode);
        return await response.Content.ReadAsByteArrayAsync();
    }
    private static HttpRequestMessage CreateDownloadRequest(Guid fileId) =>
        new(HttpMethod.Get, $"/api/v1/downloads/{fileId:D}");
    private static async Task<HttpResponseMessage> SendJsonAsync(HttpClient client, HttpMethod method, string path, object body, string? key = null)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf!.RequestToken); if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
    private sealed record CsrfResponse(string RequestToken);
    private sealed record WorkspaceResponse(Guid Id);
    private sealed record UploadResponse(Guid Id);
    private sealed record ShareCreatedResponse(Guid Id, string PublicIdentifier, string SecretToken);
}
