using TaskFlow.Domain.Users;

namespace TaskFlow.Application.Auth;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hash, string password);
}

public interface ITokenService
{
    string CreateAccessToken(User user, DateTimeOffset issuedAt, DateTimeOffset expiresAt);

    /// <summary>A new random, unguessable refresh token. Only its hash is ever stored.</summary>
    string CreateRefreshToken();
}

public sealed record RefreshToken(
    Guid Id, Guid UserId, string TokenHash, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt = null);

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Revokes the token only if it's still unrevoked, atomically. False means someone already used it.</summary>
    Task<bool> TryRevokeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);

    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>Failed logins for a normalized email, registered or not.</summary>
public sealed record LoginAttempt(string Email, int Failures, DateTimeOffset? LockedUntil);

public interface ILoginAttemptRepository
{
    Task<LoginAttempt?> GetAsync(string email, CancellationToken cancellationToken);

    Task SaveAsync(LoginAttempt attempt, CancellationToken cancellationToken);

    Task ResetAsync(string email, CancellationToken cancellationToken);
}
