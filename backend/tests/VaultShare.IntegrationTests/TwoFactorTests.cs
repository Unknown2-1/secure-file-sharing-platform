using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using VaultShare.Infrastructure.Identity;

namespace VaultShare.IntegrationTests;

public sealed class TwoFactorTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public TwoFactorTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task User_can_enable_totp_and_complete_two_factor_login()
    {
        using var client = _factory.CreateClient();
        var email = $"totp-{Guid.NewGuid():N}@example.com";
        var user = await RegisterConfirmAndLoginAsync(client, email);

        using var setup = await client.GetAsync("/api/v1/security/two-factor/setup");
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var setupPayload = await setup.Content.ReadFromJsonAsync<TwoFactorSetupResponse>();
        Assert.False(string.IsNullOrWhiteSpace(setupPayload?.SharedKey));

        var totp = new Totp(Base32Encoding.ToBytes(Assert.IsType<string>(setupPayload?.SharedKey)));
        var enableCode = totp.ComputeTotp();
        using var enable = await PostAsync(client, "/api/v1/security/two-factor/enable", new { code = enableCode });
        Assert.True(enable.StatusCode == HttpStatusCode.OK, await enable.Content.ReadAsStringAsync());
        var enabled = await enable.Content.ReadFromJsonAsync<TwoFactorEnabledResponse>();
        Assert.Equal(10, enabled?.RecoveryCodes.Count);

        using var logout = await PostAsync(client, "/api/v1/auth/logout", new { });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var passwordStep = await PostAsync(client, "/api/v1/auth/login", new
        {
            email,
            password = "ValidPass123!",
        });
        Assert.Equal(HttpStatusCode.Accepted, passwordStep.StatusCode);

        var loginCode = totp.ComputeTotp();
        using var secondFactor = await PostAsync(client, "/api/v1/auth/login/two-factor", new
        {
            code = loginCode,
            recoveryCode = (string?)null,
        });
        Assert.Equal(HttpStatusCode.OK, secondFactor.StatusCode);

        using var me = await client.GetAsync("/api/v1/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Recovery_code_login_regeneration_and_verified_disable_work()
    {
        using var client = _factory.CreateClient();
        var email = $"totp-lifecycle-{Guid.NewGuid():N}@example.com";
        await RegisterConfirmAndLoginAsync(client, email);

        var setup = await client.GetFromJsonAsync<TwoFactorSetupResponse>("/api/v1/security/two-factor/setup");
        var totp = new Totp(Base32Encoding.ToBytes(Assert.IsType<string>(setup?.SharedKey)));
        using var enable = await PostAsync(client, "/api/v1/security/two-factor/enable", new { code = totp.ComputeTotp() });
        var enabled = await enable.Content.ReadFromJsonAsync<TwoFactorEnabledResponse>();
        var recoveryCode = Assert.IsType<string>(enabled?.RecoveryCodes.First());

        _ = await PostAsync(client, "/api/v1/auth/logout", new { });
        using var passwordStep = await PostAsync(client, "/api/v1/auth/login", new { email, password = "ValidPass123!" });
        Assert.Equal(HttpStatusCode.Accepted, passwordStep.StatusCode);
        using var recoveryStep = await PostAsync(client, "/api/v1/auth/login/two-factor", new
        {
            code = (string?)null,
            recoveryCode,
        });
        Assert.Equal(HttpStatusCode.OK, recoveryStep.StatusCode);

        using var regenerate = await PostAsync(client, "/api/v1/security/two-factor/recovery-codes", new
        {
            password = "ValidPass123!",
            code = totp.ComputeTotp(),
        });
        var regenerated = await regenerate.Content.ReadFromJsonAsync<TwoFactorEnabledResponse>();
        Assert.Equal(10, regenerated?.RecoveryCodes.Count);

        using var wrongDisable = await PostAsync(client, "/api/v1/security/two-factor/disable", new
        {
            password = "WrongPass123!",
            code = totp.ComputeTotp(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongDisable.StatusCode);

        using var disable = await PostAsync(client, "/api/v1/security/two-factor/disable", new
        {
            password = "ValidPass123!",
            code = totp.ComputeTotp(),
        });
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);

        _ = await PostAsync(client, "/api/v1/auth/logout", new { });
        using var directLogin = await PostAsync(client, "/api/v1/auth/login", new { email, password = "ValidPass123!" });
        Assert.Equal(HttpStatusCode.OK, directLogin.StatusCode);
    }

    private async Task<ApplicationUser> RegisterConfirmAndLoginAsync(HttpClient client, string email)
    {
        using var register = await PostAsync(client, "/api/v1/auth/register", new
        {
            email,
            password = "ValidPass123!",
            displayName = "TOTP Test",
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        ApplicationUser user;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            user = Assert.IsType<ApplicationUser>(await users.FindByEmailAsync(email));
            var verificationToken = await users.GenerateEmailConfirmationTokenAsync(user);
            Assert.True((await users.ConfirmEmailAsync(user, verificationToken)).Succeeded);
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

    private sealed record TwoFactorSetupResponse(string SharedKey, string AuthenticatorUri);

    private sealed record TwoFactorEnabledResponse(IReadOnlyList<string> RecoveryCodes);
}
