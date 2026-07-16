using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.IntegrationTests;

public sealed class ResumableUploadTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ResumableUploadTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Exact_offset_chunks_can_resume_and_finalize_idempotently()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterConfirmLoginAndGetWorkspaceAsync(client);
        var expected = "hello resumable upload"u8.ToArray();

        using var created = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/uploads", new
        {
            workspaceId,
            filename = "../unsafe\nname.txt",
            fileSize = expected.Length,
            clientMimeType = "text/plain",
        }, "upload-create-1");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var upload = Assert.IsType<UploadResponse>(await created.Content.ReadFromJsonAsync<UploadResponse>());
        Assert.Equal("unsafe-name.txt", upload.SafeDisplayFilename);
        Assert.Equal(0, upload.UploadOffset);

        using var duplicateCreate = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/uploads", new
        {
            workspaceId,
            filename = "../unsafe\nname.txt",
            fileSize = expected.Length,
            clientMimeType = "text/plain",
        }, "upload-create-1");
        var sameUpload = await duplicateCreate.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.Equal(upload.Id, sameUpload?.Id);

        using var first = await PatchChunkAsync(client, upload.Id, 0, expected[..8]);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal("8", first.Headers.GetValues("Upload-Offset").Single());
        using var status = await client.GetAsync($"/api/v1/uploads/{upload.Id:D}");
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.Equal(8, (await status.Content.ReadFromJsonAsync<UploadResponse>())?.UploadOffset);

        using var stale = await PatchChunkAsync(client, upload.Id, 0, expected[8..]);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var second = await PatchChunkAsync(client, upload.Id, 8, expected[8..]);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        using var finalized = await SendJsonAsync(client, HttpMethod.Post, $"/api/v1/uploads/{upload.Id:D}/finalize", new { }, "upload-finalize-1");
        Assert.Equal(HttpStatusCode.OK, finalized.StatusCode);
        using var duplicate = await SendJsonAsync(client, HttpMethod.Post, $"/api/v1/uploads/{upload.Id:D}/finalize", new { }, "upload-finalize-1");
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        var stored = await db.FileUploads.AsNoTracking().SingleAsync(candidate => candidate.Id == upload.Id);
        Assert.Equal(expected, await File.ReadAllBytesAsync(stored.TemporaryPath));
        Assert.Equal(expected.Length, stored.UploadOffset);
        Assert.Equal("Uploaded", stored.Status.ToString());
        File.Delete(stored.TemporaryPath);
    }

    [Fact]
    public async Task Oversized_or_invalid_upload_metadata_is_rejected()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterConfirmLoginAndGetWorkspaceAsync(client);
        using var oversized = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/uploads", new
        {
            workspaceId,
            filename = "large.bin",
            fileSize = 1_073_741_825L,
            clientMimeType = "application/octet-stream",
        }, "upload-oversized");
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);

        using var nullByte = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/uploads", new
        {
            workspaceId,
            filename = "unsafe\0name.txt",
            fileSize = 10,
            clientMimeType = "text/plain",
        }, "upload-null-byte");
        Assert.Equal(HttpStatusCode.BadRequest, nullByte.StatusCode);
    }

    [Fact]
    public async Task Owner_can_cancel_incomplete_upload_idempotently_and_plaintext_is_removed()
    {
        using var client = _factory.CreateClient();
        var workspaceId = await RegisterConfirmLoginAndGetWorkspaceAsync(client);
        using var created = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/uploads", new
        {
            workspaceId,
            filename = "cancel.txt",
            fileSize = 10,
            clientMimeType = "text/plain",
        }, "upload-cancel-create");
        var upload = Assert.IsType<UploadResponse>(await created.Content.ReadFromJsonAsync<UploadResponse>());

        using var cancel = await SendJsonAsync(client, HttpMethod.Delete, $"/api/v1/uploads/{upload.Id:D}", new { }, "upload-cancel-request");
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        using var duplicate = await SendJsonAsync(client, HttpMethod.Delete, $"/api/v1/uploads/{upload.Id:D}", new { }, "upload-cancel-request");
        Assert.Equal(HttpStatusCode.NoContent, duplicate.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        var stored = await db.FileUploads.AsNoTracking().SingleAsync(candidate => candidate.Id == upload.Id);
        Assert.Equal("Abandoned", stored.Status.ToString());
        Assert.False(File.Exists(stored.TemporaryPath));
    }

    private async Task<Guid> RegisterConfirmLoginAndGetWorkspaceAsync(HttpClient client)
    {
        var email = $"upload-{Guid.NewGuid():N}@example.com";
        using var register = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/auth/register", new
        {
            email,
            password = "ValidPass123!",
            displayName = "Upload Test",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            Assert.True((await users.ConfirmEmailAsync(user, await users.GenerateEmailConfirmationTokenAsync(user))).Succeeded);
        }

        using var login = await SendJsonAsync(client, HttpMethod.Post, "/api/v1/auth/login", new { email, password = "ValidPass123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var workspaces = await client.GetFromJsonAsync<WorkspaceResponse[]>("/api/v1/workspaces");
        return Assert.Single(Assert.IsType<WorkspaceResponse[]>(workspaces)).Id;
    }

    private static async Task<HttpResponseMessage> PatchChunkAsync(HttpClient client, Guid uploadId, long offset, byte[] bytes)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/uploads/{uploadId:D}")
        {
            Content = new ByteArrayContent(bytes),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        request.Headers.Add("Upload-Offset", offset.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("X-CSRF-TOKEN", Assert.IsType<string>(csrf?.RequestToken));
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(HttpClient client, HttpMethod method, string path, object body, string? idempotencyKey = null)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", Assert.IsType<string>(csrf?.RequestToken));
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string RequestToken);
    private sealed record WorkspaceResponse(Guid Id);
    private sealed record UploadResponse(Guid Id, string SafeDisplayFilename, long UploadOffset);
}
