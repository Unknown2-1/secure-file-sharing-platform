using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Infrastructure.Identity;

namespace VaultShare.IntegrationTests;

public sealed class WorkspaceAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public WorkspaceAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task User_cannot_read_another_users_personal_workspace()
    {
        using var ownerClient = _factory.CreateClient();
        await RegisterAndLoginAsync(ownerClient, $"owner-{Guid.NewGuid():N}@example.com", "Owner Test");

        var ownerWorkspaces = await ownerClient.GetFromJsonAsync<WorkspaceResponse[]>("/api/v1/workspaces");
        var ownerWorkspace = Assert.Single(Assert.IsType<WorkspaceResponse[]>(ownerWorkspaces));

        using var otherClient = _factory.CreateClient();
        await RegisterAndLoginAsync(otherClient, $"other-{Guid.NewGuid():N}@example.com", "Other Test");

        using var denied = await otherClient.GetAsync($"/api/v1/workspaces/{ownerWorkspace.Id:D}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        using var allowed = await ownerClient.GetAsync($"/api/v1/workspaces/{ownerWorkspace.Id:D}");
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    private async Task RegisterAndLoginAsync(HttpClient client, string email, string displayName)
    {
        var csrf = await GetCsrfAsync(client);
        using var register = await SendAsync(client, "/api/v1/auth/register", csrf, new
        {
            email,
            password = "ValidPass123!",
            displayName,
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            Assert.True((await users.ConfirmEmailAsync(user, token)).Succeeded);
        }

        csrf = await GetCsrfAsync(client);
        using var login = await SendAsync(client, "/api/v1/auth/login", csrf, new
        {
            email,
            password = "ValidPass123!",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static async Task<string> GetCsrfAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        return Assert.IsType<string>(response?.RequestToken);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string csrf, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        return client.SendAsync(request);
    }

    private sealed record CsrfResponse(string RequestToken);

    private sealed record WorkspaceResponse(Guid Id, string Name, string Role, DateTimeOffset CreatedAt);
}
