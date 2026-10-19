using NSubstitute;
using TaskFlow.Application.Auth;

namespace TaskFlow.Application.Tests.Auth;

public class AuthServiceLoginTests : AuthServiceTestBase
{
    private Task<Domain.Common.Result<AuthTokens>> LoginAsync(string email, string password = Password)
        => Service.LoginAsync(new(email, password), default);

    private async Task FailTimesAsync(int times, string email)
    {
        for (var i = 0; i < times; i++)
            Assert.Equal([AuthErrors.InvalidCredentials], (await LoginAsync(email, "wrong")).Errors);
    }

    [Fact]
    public async Task Right_credentials_log_in_whatever_the_email_case_or_spaces()
    {
        await RegisterAsync("john@example.com");

        var result = await LoginAsync("  JOHN@Example.com ");

        Assert.Equal($"access:{UserWith("john@example.com").Id}", result.Value.AccessToken);
        Assert.Equal(2, RefreshTokens.Tokens.Count);
    }

    [Theory]
    [InlineData("john@example.com", "Wr0ng!pass")]
    [InlineData("nobody@example.com", Password)]
    [InlineData("not-an-email", Password)]
    [InlineData(null, null)]
    public async Task Wrong_password_unknown_email_and_bad_input_all_get_the_same_error(string? email, string? password)
    {
        await RegisterAsync("john@example.com");

        var result = await Service.LoginAsync(new(email, password), default);

        Assert.Equal([AuthErrors.InvalidCredentials], result.Errors);
    }

    [Fact]
    public async Task Unknown_email_still_checks_a_password_hash_so_timing_doesnt_reveal_it()
    {
        await LoginAsync("nobody@example.com", "Some!pass1");

        Hasher.Received(1).Verify(Arg.Any<string>(), "Some!pass1");
    }

    [Fact]
    public async Task Three_failures_lock_the_email_even_against_the_right_password()
    {
        await RegisterAsync("john@example.com");

        await FailTimesAsync(1, "john@example.com");
        await FailTimesAsync(1, " JOHN@example.com");
        await FailTimesAsync(1, "John@Example.com ");

        Assert.Equal([AuthErrors.LockedOut], (await LoginAsync("john@example.com")).Errors);
    }

    [Fact]
    public async Task Two_failures_dont_lock()
    {
        await RegisterAsync("john@example.com");
        await FailTimesAsync(2, "john@example.com");

        Assert.True((await LoginAsync("john@example.com")).IsSuccess);
    }

    [Fact]
    public async Task Lock_lasts_exactly_one_minute()
    {
        await RegisterAsync("john@example.com");
        await FailTimesAsync(3, "john@example.com");

        Time.Advance(TimeSpan.FromMinutes(1) - TimeSpan.FromTicks(1));
        Assert.Equal([AuthErrors.LockedOut], (await LoginAsync("john@example.com")).Errors);

        Time.Advance(TimeSpan.FromTicks(1));
        Assert.True((await LoginAsync("john@example.com")).IsSuccess);
    }

    [Fact]
    public async Task Attempts_while_locked_dont_extend_the_lock_or_check_the_password()
    {
        await RegisterAsync("john@example.com");
        await FailTimesAsync(3, "john@example.com");
        Hasher.ClearReceivedCalls();

        Time.Advance(TimeSpan.FromSeconds(30));
        await LoginAsync("john@example.com", "wrong");
        Hasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);

        Time.Advance(TimeSpan.FromSeconds(30));
        Assert.True((await LoginAsync("john@example.com")).IsSuccess);
    }

    [Fact]
    public async Task Unregistered_email_is_locked_the_same_way_so_the_lock_reveals_nothing()
    {
        await FailTimesAsync(3, "nobody@example.com");

        Assert.Equal([AuthErrors.LockedOut], (await LoginAsync("nobody@example.com")).Errors);
    }

    [Fact]
    public async Task Successful_login_resets_the_failure_count()
    {
        await RegisterAsync("john@example.com");

        await FailTimesAsync(2, "john@example.com");
        Assert.True((await LoginAsync("john@example.com")).IsSuccess);
        await FailTimesAsync(2, "john@example.com");

        Assert.True((await LoginAsync("john@example.com")).IsSuccess);
    }

    [Fact]
    public async Task Lock_only_applies_to_that_email()
    {
        await RegisterAsync("john@example.com");
        await RegisterAsync("jane@example.com");
        await FailTimesAsync(3, "john@example.com");

        Assert.True((await LoginAsync("jane@example.com")).IsSuccess);
    }
}
