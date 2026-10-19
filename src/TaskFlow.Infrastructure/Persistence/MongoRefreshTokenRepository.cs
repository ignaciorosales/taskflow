using MongoDB.Driver;
using TaskFlow.Application.Auth;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class MongoRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IMongoCollection<RefreshToken> _tokens;

    public MongoRefreshTokenRepository(IMongoDatabase database)
        => _tokens = database.GetCollection<RefreshToken>(MongoSetup.RefreshTokensCollection);

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
        => _tokens.InsertOneAsync(token, cancellationToken: cancellationToken);

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken)
        => await _tokens.Find(t => t.TokenHash == tokenHash).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryRevokeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // One conditional update, so two requests racing with the same token can't both rotate it.
        var result = await _tokens.UpdateOneAsync(
            t => t.Id == id && t.RevokedAt == null,
            Builders<RefreshToken>.Update.Set(t => t.RevokedAt, now),
            cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
        => _tokens.UpdateManyAsync(
            t => t.UserId == userId && t.RevokedAt == null,
            Builders<RefreshToken>.Update.Set(t => t.RevokedAt, now),
            cancellationToken: cancellationToken);
}
