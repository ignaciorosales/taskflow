using TaskFlow.Domain.Common;

namespace TaskFlow.Application.Auth;

public sealed class AuthService
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    public const int MaxFailedLogins = 3;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(1);

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ILoginAttemptRepository _loginAttempts;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        ILoginAttemptRepository loginAttempts,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        TimeProvider timeProvider)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _loginAttempts = loginAttempts;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    public Task<Result<AuthTokens>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<Result<AuthTokens>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<Result<AuthTokens>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    /// <summary>Revokes the refresh token if it's valid; an unknown or missing token is not an error.</summary>
    public Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
