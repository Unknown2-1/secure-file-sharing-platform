using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VaultShare.Infrastructure.Identity;

namespace VaultShare.IntegrationTests;

public sealed class PasswordResetTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PasswordResetTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Forgot_password_is_generic_and_valid_reset_changes_password()
    {
        using var client = _factory.CreateClient();
        var unknownResponse = await PostAsync(client, "/api/v1/auth/forgot-password", new
        {
            email = $"unknown-{Guid.NewGuid():N}@example.com",
        });
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);

        var email = $"reset-{Guid.NewGuid():N}@example.com";
        using var register = await PostAsync(client, "/api/v1/auth/register", new
        {
            email,
            password = "OldValidPass123!",
            displayName = "Reset Test",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        string resetToken;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            var verificationToken = await users.GenerateEmailConfirmationTokenAsync(user);
            Assert.True((await users.ConfirmEmailAsync(user, verificationToken)).Succeeded);
            resetToken = await users.GeneratePasswordResetTokenAsync(user);
        }

        using var reset = await PostAsync(client, "/api/v1/auth/reset-password", new
        {
            email,
            token = resetToken,
            newPassword = "NewValidPass123!",
        });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        using var oldLogin = await PostAsync(client, "/api/v1/auth/login", new { email, password = "OldValidPass123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        using var newLogin = await PostAsync(client, "/api/v1/auth/login", new { email, password = "NewValidPass123!" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, object body)
    {
        var csrf = await client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", Assert.IsType<string>(csrf?.RequestToken));
        return await client.SendAsync(request);
    }

    private sealed record CsrfResponse(string RequestToken);
}
