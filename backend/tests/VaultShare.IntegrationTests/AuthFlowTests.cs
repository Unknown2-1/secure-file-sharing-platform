using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Infrastructure.Identity;

namespace VaultShare.IntegrationTests;

public sealed class AuthFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthFlowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Registration_without_antiforgery_token_is_rejected()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email = $"csrf-{Guid.NewGuid():N}@example.com",
            password = "ValidPass123!",
            displayName = "Pengguna Test",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task User_can_register_login_and_read_own_profile_with_cookie_session()
    {
        var email = $"auth-{Guid.NewGuid():N}@example.com";
        using var client = _factory.CreateClient();
        var csrf = await GetCsrfTokenAsync(client);

        using var register = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/auth/register", csrf, new
        {
            email,
            password = "ValidPass123!",
            displayName = "Pengguna Test",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        csrf = await GetCsrfTokenAsync(client);
        using var unverifiedLogin = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/auth/login", csrf, new
        {
            email,
            password = "ValidPass123!",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, unverifiedLogin.StatusCode);

        string verificationToken;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await users.FindByEmailAsync(email);
            verificationToken = await users.GenerateEmailConfirmationTokenAsync(Assert.IsType<ApplicationUser>(user));
        }

        csrf = await GetCsrfTokenAsync(client);
        using var verification = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/auth/verify-email", csrf, new
        {
            email,
            token = verificationToken,
        });
        Assert.Equal(HttpStatusCode.NoContent, verification.StatusCode);

        csrf = await GetCsrfTokenAsync(client);
        using var wrongLogin = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/auth/login", csrf, new
        {
            email,
            password = "WrongPass123!",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongLogin.StatusCode);

        csrf = await GetCsrfTokenAsync(client);
        using var login = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/auth/login", csrf, new
        {
            email,
            password = "ValidPass123!",
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var profile = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var payload = await profile.Content.ReadFromJsonAsync<ProfileResponse>();
        Assert.Equal(email, payload?.Email);
        Assert.Equal("Pengguna Test", payload?.DisplayName);

        csrf = await GetCsrfTokenAsync(client);
        using var logout = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/auth/logout", csrf, new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var afterLogout = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        var response = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        return Assert.IsType<string>(response?.RequestToken);
    }

    private static Task<HttpResponseMessage> SendWithCsrfAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string token,
        object body)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return client.SendAsync(request);
    }

    private sealed record CsrfResponse(string RequestToken);

    private sealed record ProfileResponse(Guid Id, string Email, string DisplayName);
}
