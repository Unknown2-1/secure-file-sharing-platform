using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Infrastructure.Identity;
using VaultShare.Infrastructure.Persistence;

namespace VaultShare.IntegrationTests;

public sealed class WorkspaceRoleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WorkspaceRoleTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Invitation_and_member_rules_enforce_owner_admin_and_viewer_boundaries()
    {
        using var ownerClient = _factory.CreateClient();
        using var adminClient = _factory.CreateClient();
        using var viewerClient = _factory.CreateClient();
        var owner = await RegisterConfirmAndLoginAsync(ownerClient, "owner-role", "Owner Role");
        var admin = await RegisterConfirmAndLoginAsync(adminClient, "admin-role", "Admin Role");
        var viewer = await RegisterConfirmAndLoginAsync(viewerClient, "viewer-role", "Viewer Role");

        var workspaces = await ownerClient.GetFromJsonAsync<WorkspaceResponse[]>("/api/v1/workspaces");
        var workspace = Assert.Single(Assert.IsType<WorkspaceResponse[]>(workspaces));

        using var inviteAdmin = await SendAsync(ownerClient, HttpMethod.Post, $"/api/v1/workspaces/{workspace.Id:D}/invitations", new
        {
            email = admin.Email,
            role = "Admin",
            expiresInHours = 24,
        });
        Assert.Equal(HttpStatusCode.Created, inviteAdmin.StatusCode);
        var adminInvitation = await inviteAdmin.Content.ReadFromJsonAsync<InvitationResponse>();
        Assert.False(string.IsNullOrWhiteSpace(adminInvitation?.SecretToken));

        await AssertTokenIsHashedAsync(Assert.IsType<InvitationResponse>(adminInvitation));
        using var acceptAdmin = await SendAsync(adminClient, HttpMethod.Post, "/api/v1/workspace-invitations/accept", new
        {
            invitationId = adminInvitation!.Id,
            secretToken = adminInvitation.SecretToken,
        });
        Assert.Equal(HttpStatusCode.NoContent, acceptAdmin.StatusCode);

        using var removeOwner = await SendAsync(adminClient, HttpMethod.Delete, $"/api/v1/workspaces/{workspace.Id:D}/members/{owner.Id:D}", null);
        Assert.Equal(HttpStatusCode.Conflict, removeOwner.StatusCode);

        using var adminInvitesAdmin = await SendAsync(adminClient, HttpMethod.Post, $"/api/v1/workspaces/{workspace.Id:D}/invitations", new
        {
            email = $"another-{Guid.NewGuid():N}@example.com",
            role = "Admin",
            expiresInHours = 24,
        });
        Assert.Equal(HttpStatusCode.Forbidden, adminInvitesAdmin.StatusCode);

        using var inviteViewer = await SendAsync(adminClient, HttpMethod.Post, $"/api/v1/workspaces/{workspace.Id:D}/invitations", new
        {
            email = viewer.Email,
            role = "Viewer",
            expiresInHours = 24,
        });
        Assert.Equal(HttpStatusCode.Created, inviteViewer.StatusCode);
        var viewerInvitation = await inviteViewer.Content.ReadFromJsonAsync<InvitationResponse>();

        using var acceptViewer = await SendAsync(viewerClient, HttpMethod.Post, "/api/v1/workspace-invitations/accept", new
        {
            invitationId = viewerInvitation!.Id,
            secretToken = viewerInvitation.SecretToken,
        });
        Assert.Equal(HttpStatusCode.NoContent, acceptViewer.StatusCode);

        using var viewerInvites = await SendAsync(viewerClient, HttpMethod.Post, $"/api/v1/workspaces/{workspace.Id:D}/invitations", new
        {
            email = $"blocked-{Guid.NewGuid():N}@example.com",
            role = "Viewer",
            expiresInHours = 24,
        });
        Assert.Equal(HttpStatusCode.Forbidden, viewerInvites.StatusCode);

        using var viewerReadsSettings = await viewerClient.GetAsync($"/api/v1/workspaces/{workspace.Id:D}/settings");
        Assert.Equal(HttpStatusCode.OK, viewerReadsSettings.StatusCode);
        var setting = new
        {
            storageQuotaBytes = 1_048_576,
            auditRetentionDays = 365,
            deletedFileGraceDays = 30,
            allowMemberPublicShares = false
        };
        using var adminChangesSecurity = await SendAsync(adminClient, HttpMethod.Put,
            $"/api/v1/workspaces/{workspace.Id:D}/settings", setting);
        Assert.Equal(HttpStatusCode.Forbidden, adminChangesSecurity.StatusCode);
        using var ownerChangesSecurity = await SendAsync(ownerClient, HttpMethod.Put,
            $"/api/v1/workspaces/{workspace.Id:D}/settings", setting);
        Assert.Equal(HttpStatusCode.OK, ownerChangesSecurity.StatusCode);
        using var overQuota = await SendAsync(ownerClient, HttpMethod.Post, "/api/v1/uploads", new
        {
            workspaceId = workspace.Id,
            filename = "over-quota.bin",
            fileSize = 1_048_577,
            clientMimeType = "application/octet-stream",
        }, "workspace-quota-test");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, overQuota.StatusCode);
        using var deletedWorkspace = await SendAsync(ownerClient, HttpMethod.Delete,
            $"/api/v1/workspaces/{workspace.Id:D}", new { }, "workspace-delete-test");
        Assert.Equal(HttpStatusCode.NoContent, deletedWorkspace.StatusCode);
        using var accessAfterDelete = await viewerClient.GetAsync($"/api/v1/workspaces/{workspace.Id:D}/settings");
        Assert.Equal(HttpStatusCode.Forbidden, accessAfterDelete.StatusCode);
    }

    private async Task<UserResponse> RegisterConfirmAndLoginAsync(HttpClient client, string prefix, string displayName)
    {
        var email = $"{prefix}-{Guid.NewGuid():N}@example.com";
        using var register = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/register", new
        {
            email,
            password = "ValidPass123!",
            displayName,
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var profile = await register.Content.ReadFromJsonAsync<UserResponse>();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            Assert.True((await users.ConfirmEmailAsync(user, token)).Succeeded);
        }

        using var login = await SendAsync(client, HttpMethod.Post, "/api/v1/auth/login", new { email, password = "ValidPass123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return Assert.IsType<UserResponse>(profile);
    }

    private async Task AssertTokenIsHashedAsync(InvitationResponse invitation)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<VaultShareDbContext>();
        var storedHash = await db.WorkspaceInvitations
            .Where(candidate => candidate.Id == invitation.Id)
            .Select(candidate => candidate.SecretTokenHash)
            .SingleAsync();
        Assert.NotEqual(invitation.SecretToken, storedHash);
        Assert.DoesNotContain(invitation.SecretToken, storedHash, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path,
        object? body, string? idempotencyKey = null)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Add("X-CSRF-TOKEN", Assert.IsType<string>(csrf?.RequestToken));
        if (idempotencyKey is not null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string RequestToken);

    private sealed record UserResponse(Guid Id, string Email, string DisplayName);

    private sealed record WorkspaceResponse(Guid Id, string Name, string Role, DateTimeOffset CreatedAt);

    private sealed record InvitationResponse(Guid Id, string SecretToken, DateTimeOffset ExpiresAt);
}
