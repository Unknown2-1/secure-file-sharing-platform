using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Infrastructure.Identity;

namespace VaultShare.IntegrationTests;

public sealed class AccountDeletionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AccountDeletionTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Account_deletion_requires_password_anonymizes_identity_and_revokes_session()
    {
        using var client = _factory.CreateClient();
        var email = $"delete-{Guid.NewGuid():N}@example.com";
        var userId = await RegisterConfirmAndLoginAsync(client, email);

        using var denied = await PostAsync(client, "/api/v1/security/delete-account", new
        {
            password = "WrongPass123!",
            twoFactorCode = (string?)null,
            recoveryCode = (string?)null,
        });
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);

        using var deleted = await PostAsync(client, "/api/v1/security/delete-account", new
        {
            password = "ValidPass123!",
            twoFactorCode = (string?)null,
            recoveryCode = (string?)null,
        });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);

        using var otherClient = _factory.CreateClient();
        using var login = await PostAsync(otherClient, "/api/v1/auth/login", new { email, password = "ValidPass123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var deletedUser = Assert.IsType<ApplicationUser>(await users.FindByIdAsync(userId.ToString()));
        Assert.Null(deletedUser.Email);
        Assert.NotNull(deletedUser.DeletedAt);
        Assert.Equal("Akun dihapus", deletedUser.DisplayName);
    }

    private async Task<Guid> RegisterConfirmAndLoginAsync(HttpClient client, string email)
    {
        using var register = await PostAsync(client, "/api/v1/auth/register", new
        {
            email,
            password = "ValidPass123!",
            displayName = "Delete Test",
        });
        var profile = await register.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            Assert.True((await users.ConfirmEmailAsync(user, token)).Succeeded);
        }

        using var login = await PostAsync(client, "/api/v1/auth/login", new { email, password = "ValidPass123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return Assert.IsType<UserResponse>(profile).Id;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", Assert.IsType<string>(csrf?.RequestToken));
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string RequestToken);

    private sealed record UserResponse(Guid Id, string Email, string DisplayName);
}
