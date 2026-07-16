using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Infrastructure.Identity;

namespace VaultShare.IntegrationTests;

public sealed class AccountLifecycleTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AccountLifecycleTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Password_and_email_changes_require_proof_and_rotate_session()
    {
        using var client = _factory.CreateClient();
        var originalEmail = $"lifecycle-{Guid.NewGuid():N}@example.com";
        var user = await RegisterConfirmAndLoginAsync(client, originalEmail);

        using var wrongPassword = await PostAsync(client, "/api/v1/security/change-password", new
        {
            currentPassword = "WrongPass123!",
            newPassword = "NewValidPass123!",
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);

        using var changedPassword = await PostAsync(client, "/api/v1/security/change-password", new
        {
            currentPassword = "ValidPass123!",
            newPassword = "NewValidPass123!",
        });
        Assert.Equal(HttpStatusCode.NoContent, changedPassword.StatusCode);

        var newEmail = $"changed-{Guid.NewGuid():N}@example.com";
        using var requestEmailChange = await PostAsync(client, "/api/v1/security/email-change/request", new { newEmail });
        Assert.Equal(HttpStatusCode.Accepted, requestEmailChange.StatusCode);

        string emailChangeToken;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var refreshed = Assert.IsType<ApplicationUser>(await users.FindByIdAsync(user.Id.ToString()));
            emailChangeToken = await users.GenerateChangeEmailTokenAsync(refreshed, newEmail);
        }

        using var confirmEmailChange = await PostAsync(client, "/api/v1/security/email-change/confirm", new
        {
            newEmail,
            token = emailChangeToken,
        });
        Assert.Equal(HttpStatusCode.NoContent, confirmEmailChange.StatusCode);

        using var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await me.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal(newEmail, profile?.Email);

        using var separateClient = _factory.CreateClient();
        using var oldCredentials = await PostAsync(separateClient, "/api/v1/auth/login", new
        {
            email = originalEmail,
            password = "ValidPass123!",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldCredentials.StatusCode);

        using var newCredentials = await PostAsync(separateClient, "/api/v1/auth/login", new
        {
            email = newEmail,
            password = "NewValidPass123!",
        });
        Assert.Equal(HttpStatusCode.OK, newCredentials.StatusCode);

        using var genericResend = await PostAsync(separateClient, "/api/v1/auth/resend-verification", new
        {
            email = $"missing-{Guid.NewGuid():N}@example.com",
        });
        Assert.Equal(HttpStatusCode.Accepted, genericResend.StatusCode);
    }

    private async Task<ApplicationUser> RegisterConfirmAndLoginAsync(HttpClient client, string email)
    {
        using var register = await PostAsync(client, "/api/v1/auth/register", new
        {
            email,
            password = "ValidPass123!",
            displayName = "Lifecycle Test",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        ApplicationUser user;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            Assert.True((await users.ConfirmEmailAsync(user, token)).Succeeded);
        }

        using var login = await PostAsync(client, "/api/v1/auth/login", new { email, password = "ValidPass123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return user;
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
