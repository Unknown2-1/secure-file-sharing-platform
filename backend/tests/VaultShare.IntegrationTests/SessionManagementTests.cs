using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Infrastructure.Identity;

namespace VaultShare.IntegrationTests;

public sealed class SessionManagementTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SessionManagementTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Revoking_current_session_blocks_subsequent_protected_requests()
    {
        using var client = _factory.CreateClient();
        var email = $"session-{Guid.NewGuid():N}@example.com";
        await RegisterAndLoginAsync(client, email);

        var sessions = await client.GetFromJsonAsync<SessionResponse[]>("/api/v1/sessions");
        var current = Assert.Single(Assert.IsType<SessionResponse[]>(sessions));
        Assert.True(current.IsCurrent);
        Assert.Null(current.RevokedAt);

        var csrf = await GetCsrfAsync(client);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/sessions/{current.Id:D}");
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        using var revoke = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);

        using var protectedRequest = await client.GetAsync("/api/v1/workspaces");
        Assert.Equal(HttpStatusCode.Unauthorized, protectedRequest.StatusCode);
    }

    private async Task RegisterAndLoginAsync(HttpClient client, string email)
    {
        var csrf = await GetCsrfAsync(client);
        using var registerRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register")
        {
            Content = JsonContent.Create(new { email, password = "ValidPass123!", displayName = "Session Test" }),
        };
        registerRequest.Headers.Add("X-CSRF-TOKEN", csrf);
        using var register = await client.SendAsync(registerRequest);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            Assert.True((await users.ConfirmEmailAsync(user, token)).Succeeded);
        }

        csrf = await GetCsrfAsync(client);
        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password = "ValidPass123!" }),
        };
        loginRequest.Headers.Add("X-CSRF-TOKEN", csrf);
        using var login = await client.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        return Assert.IsType<string>(response?.RequestToken);
    }

    private sealed record CsrfResponse(string RequestToken);

    private sealed record SessionResponse(
        Guid Id,
        DateTimeOffset CreatedAt,
        DateTimeOffset LastSeenAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? RevokedAt,
        string UserAgent,
        bool IsCurrent);
}
