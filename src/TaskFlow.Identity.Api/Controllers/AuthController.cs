using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Api.Common;
using TaskFlow.Application.Auth;

namespace TaskFlow.Identity.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    public const string RefreshCookie = "refresh_token";
    public const string RateLimitPolicy = "auth";

    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request, cancellationToken);
        return result.IsSuccess ? StartSession(result.Value, created: true) : this.ToProblem(result.Errors);
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicy)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return result.IsSuccess ? StartSession(result.Value, created: false) : this.ToProblem(result.Errors);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await auth.RefreshAsync(Request.Cookies[RefreshCookie], cancellationToken);
        if (result.IsSuccess)
            return StartSession(result.Value, created: false);

        Response.Cookies.Delete(RefreshCookie, CookieOptions(expires: null));
        return this.ToProblem(result.Errors);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookie], cancellationToken);
        Response.Cookies.Delete(RefreshCookie, CookieOptions(expires: null));
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await auth.GetMeAsync(User.UserId(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Errors);
    }

    // The refresh token never reaches JavaScript: HttpOnly cookie, sent only to /api/auth on this origin.
    private IActionResult StartSession(AuthTokens tokens, bool created)
    {
        Response.Cookies.Append(RefreshCookie, tokens.RefreshToken, CookieOptions(tokens.RefreshTokenExpiresAt));
        var body = new { accessToken = tokens.AccessToken, expiresAt = tokens.AccessTokenExpiresAt };
        return created ? Created("/api/auth/me", body) : Ok(body);
    }

    private static CookieOptions CookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires,
    };
}
