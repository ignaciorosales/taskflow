using MongoDB.Driver;
using TaskFlow.Application.Auth;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class MongoRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IMongoCollection<RefreshToken> _tokens;

    public MongoRefreshTokenRepository(IMongoDatabase database)
        => _tokens = database.GetCollection<RefreshToken>(MongoSetup.RefreshTokensCollection);

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<bool> TryRevokeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotImplementedException();
}
