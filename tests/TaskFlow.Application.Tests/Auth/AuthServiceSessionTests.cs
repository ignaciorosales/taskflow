using TaskFlow.Application.Auth;

namespace TaskFlow.Application.Tests.Auth;

public class AuthServiceSessionTests : AuthServiceTestBase
{
    [Fact]
    public async Task Refresh_rotates_the_refresh_token()
    {
        var first = await RegisterAsync();
        Time.Advance(TimeSpan.FromDays(1));

        var second = (await Service.RefreshAsync(first.RefreshToken, default)).Value;

        Assert.Equal($"access:{UserWith("john@example.com").Id}", second.AccessToken);
        Assert.Equal(Now.AddDays(1) + AuthService.AccessTokenLifetime, second.AccessTokenExpiresAt);
        Assert.Equal("refresh-2", second.RefreshToken);
        Assert.Equal(Now.AddDays(8), second.RefreshTokenExpiresAt);
        Assert.Equal(Now.AddDays(1), RefreshTokens.Tokens.Single(t => t.TokenHash == Sha256(first.RefreshToken)).RevokedAt);
        Assert.Null(RefreshTokens.Tokens.Single(t => t.TokenHash == Sha256(second.RefreshToken)).RevokedAt);
    }

    [Fact]
    public async Task Reusing_a_rotated_token_is_rejected_and_revokes_every_session_of_that_user()
    {
        var first = await RegisterAsync();
        var otherDevice = (await Service.LoginAsync(new("john@example.com", Password), default)).Value;
        var second = (await Service.RefreshAsync(first.RefreshToken, default)).Value;

        var reuse = await Service.RefreshAsync(first.RefreshToken, default);

        Assert.Equal([AuthErrors.InvalidRefreshToken], reuse.Errors);
        Assert.Equal([AuthErrors.InvalidRefreshToken], (await Service.RefreshAsync(second.RefreshToken, default)).Errors);
        Assert.Equal([AuthErrors.InvalidRefreshToken], (await Service.RefreshAsync(otherDevice.RefreshToken, default)).Errors);
    }

    [Fact]
    public async Task Reuse_detection_doesnt_touch_other_users()
    {
        var john = await RegisterAsync("john@example.com");
        var jane = await RegisterAsync("jane@example.com");
        await Service.RefreshAsync(john.RefreshToken, default);

        await Service.RefreshAsync(john.RefreshToken, default);

        Assert.True((await Service.RefreshAsync(jane.RefreshToken, default)).IsSuccess);
    }

    [Fact]
    public async Task Refresh_token_works_until_just_before_it_expires()
    {
        var tokens = await RegisterAsync();
        Time.Advance(AuthService.RefreshTokenLifetime - TimeSpan.FromTicks(1));

        Assert.True((await Service.RefreshAsync(tokens.RefreshToken, default)).IsSuccess);
    }

    [Fact]
    public async Task Expired_refresh_token_is_rejected()
    {
        var tokens = await RegisterAsync();
        Time.Advance(AuthService.RefreshTokenLifetime);

        Assert.Equal([AuthErrors.InvalidRefreshToken], (await Service.RefreshAsync(tokens.RefreshToken, default)).Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("made-up-token")]
    public async Task Unknown_or_missing_refresh_token_is_rejected(string? token)
    {
        await RegisterAsync();

        Assert.Equal([AuthErrors.InvalidRefreshToken], (await Service.RefreshAsync(token, default)).Errors);
    }

    [Fact]
    public async Task Logout_kills_the_refresh_token()
    {
        var tokens = await RegisterAsync();

        await Service.LogoutAsync(tokens.RefreshToken, default);

        Assert.Equal([AuthErrors.InvalidRefreshToken], (await Service.RefreshAsync(tokens.RefreshToken, default)).Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("made-up-token")]
    public async Task Logout_without_a_valid_token_is_harmless(string? token)
    {
        var tokens = await RegisterAsync();

        await Service.LogoutAsync(token, default);

        Assert.True((await Service.RefreshAsync(tokens.RefreshToken, default)).IsSuccess);
    }

    [Fact]
    public async Task Me_returns_the_user()
    {
        await RegisterAsync("john@example.com");
        var user = UserWith("john@example.com");

        var me = (await Service.GetMeAsync(user.Id, default)).Value;

        Assert.Equal(new UserDto(user.Id, "john@example.com"), me);
    }

    [Fact]
    public async Task Me_for_an_unknown_user_is_not_found()
    {
        Assert.Equal([AuthErrors.UserNotFound], (await Service.GetMeAsync(Guid.NewGuid(), default)).Errors);
    }
}
