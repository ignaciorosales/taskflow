using System.Security.Cryptography;
using System.Text;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Users;

namespace TaskFlow.Application.Auth;

public sealed class AuthService
{
    public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);
    public const int MaxFailedLogins = 3;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(1);

    // Checked when the email is unknown, so a miss takes as long as a wrong password.
    private static string? s_dummyHash;

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

    public async Task<Result<AuthTokens>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = Email.Create(request.Email);
        Error[] errors = [.. email.Errors, .. PasswordPolicy.Validate(request.Password)];
        if (errors.Length > 0)
            return Result<AuthTokens>.Failure(errors);

        var now = _timeProvider.GetUtcNow();
        var user = User.Create(email.Value, _passwordHasher.Hash(request.Password!), now);
        var added = await _users.AddAsync(user, cancellationToken);
        if (added.IsFailure)
            return Result<AuthTokens>.Failure([.. added.Errors]);

        return Result<AuthTokens>.Success(await IssueTokensAsync(user, now, cancellationToken));
    }

    public async Task<Result<AuthTokens>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        // Keyed on what was typed, normalized, so unregistered and malformed emails lock just the same.
        var key = (request.Email ?? "").Trim().ToLowerInvariant();
        var attempt = await _loginAttempts.GetAsync(key, cancellationToken);
        if (attempt?.LockedUntil > now)
            return Result<AuthTokens>.Failure(AuthErrors.LockedOut);

        var email = Email.Create(request.Email);
        var user = email.IsSuccess ? await _users.GetByEmailAsync(email.Value, cancellationToken) : null;
        var passwordMatches = _passwordHasher.Verify(user?.PasswordHash ?? DummyHash(), request.Password ?? "");

        if (user is null || !passwordMatches)
        {
            await RecordFailureAsync(key, attempt, now, cancellationToken);
            return Result<AuthTokens>.Failure(AuthErrors.InvalidCredentials);
        }

        if (attempt is not null)
            await _loginAttempts.ResetAsync(key, cancellationToken);
        return Result<AuthTokens>.Success(await IssueTokensAsync(user, now, cancellationToken));
    }

    public async Task<Result<AuthTokens>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return Result<AuthTokens>.Failure(AuthErrors.InvalidRefreshToken);

        var now = _timeProvider.GetUtcNow();
        var stored = await _refreshTokens.GetByHashAsync(Hash(refreshToken), cancellationToken);
        if (stored is null || stored.ExpiresAt <= now)
            return Result<AuthTokens>.Failure(AuthErrors.InvalidRefreshToken);

        if (!await _refreshTokens.TryRevokeAsync(stored.Id, now, cancellationToken))
        {
            // This token was already used: assume it was stolen and end every session of its user.
            await _refreshTokens.RevokeAllForUserAsync(stored.UserId, now, cancellationToken);
            return Result<AuthTokens>.Failure(AuthErrors.InvalidRefreshToken);
        }

        var user = await _users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null)
            return Result<AuthTokens>.Failure(AuthErrors.InvalidRefreshToken);

        return Result<AuthTokens>.Success(await IssueTokensAsync(user, now, cancellationToken));
    }

    /// <summary>Revokes the refresh token if it's valid; an unknown or missing token is not an error.</summary>
    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(refreshToken))
            return;

        var stored = await _refreshTokens.GetByHashAsync(Hash(refreshToken), cancellationToken);
        if (stored is not null)
            await _refreshTokens.TryRevokeAsync(stored.Id, _timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        return user is null
            ? Result<UserDto>.Failure(AuthErrors.UserNotFound)
            : Result<UserDto>.Success(new UserDto(user.Id, user.Email.Value));
    }

    // Known limit: read-then-write, so parallel failures can slip past the limit by a few; the per-IP rate limit caps that.
    private Task RecordFailureAsync(string key, LoginAttempt? attempt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var failures = (attempt?.Failures ?? 0) + 1;
        return _loginAttempts.SaveAsync(
            failures >= MaxFailedLogins ? new LoginAttempt(key, 0, now + LockoutDuration) : new LoginAttempt(key, failures, null),
            cancellationToken);
    }

    private async Task<AuthTokens> IssueTokensAsync(User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var refreshToken = _tokenService.CreateRefreshToken();
        await _refreshTokens.AddAsync(
            new RefreshToken(Guid.NewGuid(), user.Id, Hash(refreshToken), now, now + RefreshTokenLifetime),
            cancellationToken);

        return new AuthTokens(
            _tokenService.CreateAccessToken(user, now, now + AccessTokenLifetime),
            now + AccessTokenLifetime,
            refreshToken,
            now + RefreshTokenLifetime);
    }

    private string DummyHash() => s_dummyHash ??= _passwordHasher.Hash(Guid.NewGuid().ToString());

    private static string Hash(string refreshToken) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
