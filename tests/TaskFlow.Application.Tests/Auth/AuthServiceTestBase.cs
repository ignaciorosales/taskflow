using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Users;

namespace TaskFlow.Application.Tests.Auth;

public abstract class AuthServiceTestBase
{
    protected static readonly DateTimeOffset Now = new(2030, 6, 1, 9, 0, 0, TimeSpan.Zero);
    protected const string Password = "Secr3t!pass";

    protected readonly FakeTimeProvider Time = new(Now);
    protected readonly InMemoryUserRepository Users = new();
    protected readonly InMemoryRefreshTokenRepository RefreshTokens = new();
    protected readonly InMemoryLoginAttemptRepository LoginAttempts = new();
    protected readonly IPasswordHasher Hasher = Substitute.For<IPasswordHasher>();
    protected readonly ITokenService TokenService = Substitute.For<ITokenService>();
    protected readonly AuthService Service;

    protected AuthServiceTestBase()
    {
        Hasher.Hash(Arg.Any<string>()).Returns(call => "hashed:" + call.Arg<string>());
        Hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(0) == "hashed:" + call.ArgAt<string>(1));
        TokenService.CreateAccessToken(Arg.Any<User>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>())
            .Returns(call => $"access:{call.Arg<User>().Id}");
        var issued = 0;
        TokenService.CreateRefreshToken().Returns(_ => $"refresh-{++issued}");

        Service = new AuthService(Users, RefreshTokens, LoginAttempts, Hasher, TokenService, Time);
    }

    protected static string Sha256(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    protected async Task<AuthTokens> RegisterAsync(string email = "john@example.com")
        => (await Service.RegisterAsync(new(email, Password), default)).Value;

    protected User UserWith(string email) => Users.Users.Single(u => u.Email.Value == email);
}
