using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VaultShare.SecurityTests;

public sealed class SecurityHeaderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SecurityHeaderTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Testing")).CreateClient();
    }

    [Fact]
    public async Task Api_response_has_baseline_security_headers()
    {
        using var response = await _client.GetAsync("/health/live");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("camera=()", response.Headers.GetValues("Permissions-Policy").Single());
        Assert.Equal("same-origin", response.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
        Assert.Equal("same-site", response.Headers.GetValues("Cross-Origin-Resource-Policy").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Problem_details_and_response_expose_same_safe_correlation_identifier()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/public/shares/access")
        {
            Content = JsonContent.Create(new { }),
        };
        request.Headers.Add("X-Correlation-ID", "safe-correlation-123");
        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("safe-correlation-123", response.Headers.GetValues("X-Correlation-ID").Single());
        Assert.Contains("\"correlationId\":\"safe-correlation-123\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_endpoint_is_rate_limited()
    {
        var csrf = await _client.GetFromJsonAsync<CsrfResponse>("/api/v1/auth/csrf");
        var token = Assert.IsType<string>(csrf?.RequestToken);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            using var request = CreateLoginRequest(token, attempt);
            using var response = await _client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        using var limitedRequest = CreateLoginRequest(token, 11);
        using var limited = await _client.SendAsync(limitedRequest);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Cors_allows_only_configured_frontend_with_credentials()
    {
        using var allowedRequest = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        allowedRequest.Headers.Add("Origin", "http://localhost:3000");
        using var allowed = await _client.SendAsync(allowedRequest);
        Assert.Equal("http://localhost:3000", allowed.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", allowed.Headers.GetValues("Access-Control-Allow-Credentials").Single());

        using var deniedRequest = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        deniedRequest.Headers.Add("Origin", "https://attacker.example");
        using var denied = await _client.SendAsync(deniedRequest);
        Assert.False(denied.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static HttpRequestMessage CreateLoginRequest(string csrfToken, int attempt)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = $"missing-{attempt}@example.com",
                password = "InvalidPass123!",
            }),
        };
        request.Headers.Add("X-CSRF-TOKEN", csrfToken);
        return request;
    }

    private sealed record CsrfResponse(string RequestToken);
}
