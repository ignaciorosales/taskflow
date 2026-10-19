using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Users;

namespace TaskFlow.Infrastructure.Security;

public sealed record JwtOptions(string Key, string Issuer, string Audience)
{
    public const int MinKeyBytes = 32;

    /// <summary>Reads Jwt:Key (env var Jwt__Key) and refuses to go on without a key strong enough for HS256.</summary>
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var key = configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < MinKeyBytes)
            throw new InvalidOperationException(
                $"Jwt:Key must be set (environment variable Jwt__Key) to at least {MinKeyBytes} bytes.");

        return new JwtOptions(key, configuration["Jwt:Issuer"] ?? "taskflow", configuration["Jwt:Audience"] ?? "taskflow");
    }

    public SymmetricSecurityKey SigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

public sealed class JwtTokenService : ITokenService
{
    private readonly JwtOptions _options;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenService(JwtOptions options) => _options = options;

    public string CreateAccessToken(User user, DateTimeOffset issuedAt, DateTimeOffset expiresAt)
        => _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                [JwtRegisteredClaimNames.Email] = user.Email.Value,
            },
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(_options.SigningKey(), SecurityAlgorithms.HmacSha256),
        });

    public string CreateRefreshToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
}
