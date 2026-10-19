using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MongoDb;

namespace TaskFlow.Identity.Api.Tests;

/// <summary>The real Identity API on a real Mongo container, one per test class.</summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string JwtKey = "integration-test-signing-key-at-least-32-bytes";

    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:8.0").Build();

    public Task InitializeAsync() => _mongo.StartAsync();

    Task IAsyncLifetime.DisposeAsync() => Task.WhenAll(DisposeAsync().AsTask(), _mongo.DisposeAsync().AsTask());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Mongo:ConnectionString", _mongo.GetConnectionString());
        builder.UseSetting("Mongo:Database", "taskflow_tests");
        builder.UseSetting("Jwt:Key", JwtKey);
    }

    /// <summary>A client that looks like its own browser: own IP for rate limiting, cookies handled by hand.</summary>
    public HttpClient ClientFromNewIp()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.{Random.Shared.Next(256)}.{Random.Shared.Next(256)}.{Random.Shared.Next(1, 255)}");
        return client;
    }

    public static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";
}

public sealed record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);

public static class AuthClientExtensions
{
    public const string Password = "Secr3t!pass";

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, string email, string password = Password)
        => client.PostAsJsonAsync("/api/auth/register", new { email, password });

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = Password)
        => client.PostAsJsonAsync("/api/auth/login", new { email, password });

    public static Task<HttpResponseMessage> SendWithCookieAsync(this HttpClient client, HttpMethod method, string url, string? refreshToken)
    {
        var request = new HttpRequestMessage(method, url);
        if (refreshToken is not null)
            request.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        return client.SendAsync(request);
    }

    /// <summary>The raw Set-Cookie header for the refresh token, or null.</summary>
    public static string? RefreshCookie(this HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith("refresh_token=", StringComparison.Ordinal))
            : null;

    public static string RefreshTokenValue(this HttpResponseMessage response)
        => response.RefreshCookie()!.Split(';')[0]["refresh_token=".Length..];
}
