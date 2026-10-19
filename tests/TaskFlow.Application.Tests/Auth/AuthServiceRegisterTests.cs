using NSubstitute;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Users;

namespace TaskFlow.Application.Tests.Auth;

public class AuthServiceRegisterTests : AuthServiceTestBase
{
    [Fact]
    public async Task Register_stores_the_user_with_a_hashed_password_and_logs_them_in()
    {
        var result = await Service.RegisterAsync(new("  John@Example.com ", Password), default);

        var user = UserWith("john@example.com");
        Assert.Equal("hashed:" + Password, user.PasswordHash);
        Assert.Equal(Now, user.CreatedAt);

        var tokens = result.Value;
        Assert.Equal($"access:{user.Id}", tokens.AccessToken);
        Assert.Equal(Now + AuthService.AccessTokenLifetime, tokens.AccessTokenExpiresAt);
        Assert.Equal("refresh-1", tokens.RefreshToken);
        Assert.Equal(Now + AuthService.RefreshTokenLifetime, tokens.RefreshTokenExpiresAt);
        TokenService.Received(1).CreateAccessToken(user, Now, Now.AddMinutes(15));
    }

    [Fact]
    public async Task Only_a_hash_of_the_refresh_token_is_stored()
    {
        var tokens = await RegisterAsync();

        var stored = Assert.Single(RefreshTokens.Tokens);
        Assert.Equal(Sha256(tokens.RefreshToken), stored.TokenHash);
        Assert.NotEqual(tokens.RefreshToken, stored.TokenHash);
        Assert.Equal(UserWith("john@example.com").Id, stored.UserId);
        Assert.Equal(Now.AddDays(7), stored.ExpiresAt);
        Assert.Null(stored.RevokedAt);
    }

    [Fact]
    public async Task Password_is_hashed_exactly_as_typed()
    {
        await Service.RegisterAsync(new("john@example.com", "  Secr3t!pass  "), default);

        Hasher.Received(1).Hash("  Secr3t!pass  ");
    }

    [Fact]
    public async Task Taken_email_returns_EmailTaken_and_issues_no_tokens()
    {
        await RegisterAsync("john@example.com");

        var result = await Service.RegisterAsync(new(" JOHN@example.com ", Password), default);

        Assert.Equal([UserErrors.EmailTaken], result.Errors);
        Assert.Single(RefreshTokens.Tokens);
    }

    [Fact]
    public async Task Email_and_password_errors_are_reported_together_and_nothing_is_stored()
    {
        var result = await Service.RegisterAsync(new("not-an-email", "short"), default);

        Assert.Equal(
            [UserErrors.EmailInvalid, AuthErrors.PasswordTooShort, AuthErrors.PasswordNeedsDigit, AuthErrors.PasswordNeedsSymbol],
            result.Errors);
        Assert.Empty(Users.Users);
        Assert.Empty(RefreshTokens.Tokens);
    }
}
