using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Domain.Users;
using TaskFlow.Infrastructure.Security;

namespace TaskFlow.Infrastructure.Tests.Security;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new("a-test-signing-key-that-is-at-least-32-bytes", "taskflow", "taskflow-api");
    private static readonly DateTimeOffset IssuedAt = new(2030, 6, 1, 9, 0, 0, TimeSpan.Zero);
    private readonly JwtTokenService _service = new(Options);
    private readonly User _user = User.Create(Email.Create("john@example.com").Value, "hash", IssuedAt);

    private static Task<TokenValidationResult> ValidateAsync(string token, string key) => new JsonWebTokenHandler().ValidateTokenAsync(
        token,
        new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            ValidateLifetime = false, // the token is dated 2030; its lifetime is checked through the claims below
        });

    [Fact]
    public async Task Access_token_is_signed_and_identifies_the_user()
    {
        var token = _service.CreateAccessToken(_user, IssuedAt, IssuedAt.AddMinutes(15));

        var result = await ValidateAsync(token, Options.Key);

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(_user.Id.ToString(), result.Claims[JwtRegisteredClaimNames.Sub]);
        Assert.Equal("john@example.com", result.Claims[JwtRegisteredClaimNames.Email]);
    }

    [Fact]
    public void Access_token_carries_the_given_lifetime()
    {
        var token = new JsonWebToken(_service.CreateAccessToken(_user, IssuedAt, IssuedAt.AddMinutes(15)));

        Assert.Equal(IssuedAt.UtcDateTime, token.IssuedAt);
        Assert.Equal(IssuedAt.AddMinutes(15).UtcDateTime, token.ValidTo);
    }

    [Fact]
    public async Task Access_token_fails_validation_with_another_key()
    {
        var token = _service.CreateAccessToken(_user, IssuedAt, IssuedAt.AddMinutes(15));

        Assert.False((await ValidateAsync(token, "some-other-signing-key-that-is-32-bytes!!")).IsValid);
    }

    [Fact]
    public void Refresh_tokens_are_long_url_safe_and_unique()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => _service.CreateRefreshToken()).ToList();

        Assert.Equal(100, tokens.Distinct().Count());
        Assert.All(tokens, token =>
        {
            Assert.True(token.Length >= 43, "at least 256 bits of entropy");
            Assert.Matches("^[A-Za-z0-9_-]+$", token);
        });
    }
}
