using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace TaskFlow.Identity.Api.Tests;

public class AuthEndpointsTests(IdentityApiFactory factory) : IClassFixture<IdentityApiFactory>
{
    private readonly HttpClient _client = factory.ClientFromNewIp();

    private async Task<HttpResponseMessage> RegisteredAsync(string? email = null)
    {
        var response = await _client.RegisterAsync(email ?? IdentityApiFactory.NewEmail());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response;
    }

    private static async Task<ProblemDetails> ProblemAsync(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;

    [Fact]
    public async Task Register_returns_201_with_an_access_token_and_a_locked_down_refresh_cookie()
    {
        var response = await _client.RegisterAsync(IdentityApiFactory.NewEmail());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);

        var cookie = response.RefreshCookie()!.ToLowerInvariant();
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
    }

    [Fact]
    public async Task Registering_a_taken_email_returns_409_with_the_email_error()
    {
        var email = IdentityApiFactory.NewEmail();
        await RegisteredAsync(email);

        var response = await _client.RegisterAsync(email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("email", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Invalid_registration_returns_400_with_errors_per_field()
    {
        var response = await _client.RegisterAsync("not-an-email", "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Equal(["Email is not valid."], problem!.Errors["email"]);
        Assert.Equal(3, problem.Errors["password"].Length);
    }

    [Fact]
    public async Task Login_returns_200_with_an_access_token_and_a_refresh_cookie()
    {
        var email = IdentityApiFactory.NewEmail();
        await RegisteredAsync(email);

        var response = await _client.LoginAsync(email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty((await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken));
        Assert.NotNull(response.RefreshCookie());
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_identical_401s()
    {
        var email = IdentityApiFactory.NewEmail();
        await RegisteredAsync(email);

        var wrongPassword = await _client.LoginAsync(email, "Wr0ng!pass");
        var unknownEmail = await _client.LoginAsync(IdentityApiFactory.NewEmail());

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var (a, b) = (await ProblemAsync(wrongPassword), await ProblemAsync(unknownEmail));
        Assert.Equal((a.Title, a.Detail, a.Type), (b.Title, b.Detail, b.Type));
    }

    [Fact]
    public async Task Fourth_login_after_three_failures_is_429_even_with_the_right_password()
    {
        var email = IdentityApiFactory.NewEmail();
        await RegisteredAsync(email);
        for (var i = 0; i < 3; i++)
            await _client.LoginAsync(email, "Wr0ng!pass");

        Assert.Equal(HttpStatusCode.TooManyRequests, (await _client.LoginAsync(email)).StatusCode);
    }

    [Fact]
    public async Task Login_and_register_are_rate_limited_per_ip()
    {
        for (var i = 0; i < 10; i++)
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await _client.LoginAsync(IdentityApiFactory.NewEmail())).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await _client.LoginAsync(IdentityApiFactory.NewEmail())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await _client.RegisterAsync(IdentityApiFactory.NewEmail())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await factory.ClientFromNewIp().RegisterAsync(IdentityApiFactory.NewEmail())).StatusCode);
    }

    [Fact]
    public async Task Refresh_returns_a_new_access_token_and_rotates_the_cookie()
    {
        var registered = await RegisteredAsync();

        var response = await _client.SendWithCookieAsync(HttpMethod.Post, "/api/auth/refresh", registered.RefreshTokenValue());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty((await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken));
        Assert.NotEqual(registered.RefreshTokenValue(), response.RefreshTokenValue());
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_cookie_is_401_and_kills_the_new_one_too()
    {
        var registered = await RegisteredAsync();
        var rotated = await _client.SendWithCookieAsync(HttpMethod.Post, "/api/auth/refresh", registered.RefreshTokenValue());

        var reuse = await _client.SendWithCookieAsync(HttpMethod.Post, "/api/auth/refresh", registered.RefreshTokenValue());
        var afterReuse = await _client.SendWithCookieAsync(HttpMethod.Post, "/api/auth/refresh", rotated.RefreshTokenValue());

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_is_401()
    {
        var response = await _client.SendWithCookieAsync(HttpMethod.Post, "/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_returns_204_clears_the_cookie_and_ends_the_session()
    {
        var registered = await RegisteredAsync();
        var token = registered.RefreshTokenValue();

        var logout = await _client.SendWithCookieAsync(HttpMethod.Post, "/api/auth/logout", token);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Matches("(?i)expires=thu, 01 jan 1970", logout.RefreshCookie()!);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await _client.SendWithCookieAsync(HttpMethod.Post, "/api/auth/refresh", token)).StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_user_behind_the_access_token()
    {
        var email = IdentityApiFactory.NewEmail();
        var registered = await RegisteredAsync(email);
        var accessToken = (await registered.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal(email, me!["email"]);
        Assert.True(Guid.TryParse(me["id"], out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.forged-signature")]
    public async Task Me_without_a_valid_access_token_is_401(string? token)
    {
        if (token is not null)
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Health_is_public_and_healthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
