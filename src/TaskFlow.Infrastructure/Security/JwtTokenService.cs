using TaskFlow.Application.Auth;
using TaskFlow.Domain.Users;

namespace TaskFlow.Infrastructure.Security;

public sealed record JwtOptions(string Key, string Issuer, string Audience);

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(JwtOptions options) => _options = options;

    public string CreateAccessToken(User user, DateTimeOffset issuedAt, DateTimeOffset expiresAt) => throw new NotImplementedException();

    public string CreateRefreshToken() => throw new NotImplementedException();
}
