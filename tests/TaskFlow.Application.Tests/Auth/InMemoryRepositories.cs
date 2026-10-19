using TaskFlow.Application.Auth;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Users;

namespace TaskFlow.Application.Tests.Auth;

// Stateful fakes: lockout and token rotation span many calls, which mocks would make unreadable.

public sealed class InMemoryUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<Result> AddAsync(User user, CancellationToken cancellationToken)
    {
        if (Users.Any(u => u.Email == user.Email))
            return Task.FromResult(Result.Failure(UserErrors.EmailTaken));
        Users.Add(user);
        return Task.FromResult(Result.Success());
    }

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken)
        => Task.FromResult(Users.SingleOrDefault(u => u.Email == email));

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(Users.SingleOrDefault(u => u.Id == id));
}

public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    public List<RefreshToken> Tokens { get; } = [];

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
        => Task.FromResult(Tokens.SingleOrDefault(t => t.TokenHash == tokenHash));

    public Task<bool> TryRevokeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var index = Tokens.FindIndex(t => t.Id == id && t.RevokedAt is null);
        if (index < 0)
            return Task.FromResult(false);
        Tokens[index] = Tokens[index] with { RevokedAt = now };
        return Task.FromResult(true);
    }

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        for (var i = 0; i < Tokens.Count; i++)
            if (Tokens[i].UserId == userId && Tokens[i].RevokedAt is null)
                Tokens[i] = Tokens[i] with { RevokedAt = now };
        return Task.CompletedTask;
    }
}

public sealed class InMemoryLoginAttemptRepository : ILoginAttemptRepository
{
    public Dictionary<string, LoginAttempt> Attempts { get; } = [];

    public Task<LoginAttempt?> GetAsync(string email, CancellationToken cancellationToken)
        => Task.FromResult(Attempts.GetValueOrDefault(email));

    public Task SaveAsync(LoginAttempt attempt, CancellationToken cancellationToken)
    {
        Attempts[attempt.Email] = attempt;
        return Task.CompletedTask;
    }

    public Task ResetAsync(string email, CancellationToken cancellationToken)
    {
        Attempts.Remove(email);
        return Task.CompletedTask;
    }
}
