using MongoDB.Bson;
using MongoDB.Driver;
using TaskFlow.Application.Auth;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Tests.Persistence;

public class MongoRefreshTokenRepositoryTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2030, 6, 1, 9, 0, 0, TimeSpan.Zero);

    private static RefreshToken NewToken(Guid userId, string hash)
        => new(Guid.NewGuid(), userId, hash, Now, Now.AddDays(7));

    [Fact]
    public async Task Token_is_found_by_its_hash()
    {
        var repository = new MongoRefreshTokenRepository(await mongo.NewDatabaseAsync());
        var token = NewToken(Guid.NewGuid(), "hash-1");

        await repository.AddAsync(token, default);

        Assert.Equal(token, await repository.GetByHashAsync("hash-1", default));
        Assert.Null(await repository.GetByHashAsync("hash-2", default));
    }

    [Fact]
    public async Task Token_can_be_revoked_only_once()
    {
        var repository = new MongoRefreshTokenRepository(await mongo.NewDatabaseAsync());
        var token = NewToken(Guid.NewGuid(), "hash-1");
        await repository.AddAsync(token, default);

        Assert.True(await repository.TryRevokeAsync(token.Id, Now.AddHours(1), default));
        Assert.False(await repository.TryRevokeAsync(token.Id, Now.AddHours(2), default));
        Assert.Equal(Now.AddHours(1), (await repository.GetByHashAsync("hash-1", default))!.RevokedAt);
    }

    [Fact]
    public async Task Only_one_of_many_concurrent_revocations_wins()
    {
        var database = await mongo.NewDatabaseAsync();
        var token = NewToken(Guid.NewGuid(), "hash-1");
        await new MongoRefreshTokenRepository(database).AddAsync(token, default);

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => new MongoRefreshTokenRepository(database).TryRevokeAsync(token.Id, Now, default)));

        Assert.Single(results, won => won);
    }

    [Fact]
    public async Task Revoking_all_of_a_users_tokens_leaves_other_users_alone()
    {
        var repository = new MongoRefreshTokenRepository(await mongo.NewDatabaseAsync());
        var john = Guid.NewGuid();
        var alreadyRevoked = NewToken(john, "john-old") with { RevokedAt = Now };
        await repository.AddAsync(alreadyRevoked, default);
        await repository.AddAsync(NewToken(john, "john-1"), default);
        await repository.AddAsync(NewToken(john, "john-2"), default);
        await repository.AddAsync(NewToken(Guid.NewGuid(), "jane-1"), default);

        await repository.RevokeAllForUserAsync(john, Now.AddHours(1), default);

        Assert.Equal(Now.AddHours(1), (await repository.GetByHashAsync("john-1", default))!.RevokedAt);
        Assert.Equal(Now.AddHours(1), (await repository.GetByHashAsync("john-2", default))!.RevokedAt);
        Assert.Equal(Now, (await repository.GetByHashAsync("john-old", default))!.RevokedAt);
        Assert.Null((await repository.GetByHashAsync("jane-1", default))!.RevokedAt);
    }

    [Fact]
    public async Task Mongo_deletes_tokens_once_they_expire()
    {
        var database = await mongo.NewDatabaseAsync();

        var indexes = await (await database.GetCollection<BsonDocument>(MongoSetup.RefreshTokensCollection)
            .Indexes.ListAsync()).ToListAsync();

        var ttl = Assert.Single(indexes, index => index.Contains("expireAfterSeconds"));
        Assert.Equal(0, ttl["expireAfterSeconds"].ToInt32());
        Assert.Equal(new BsonDocument("expiresAt", 1), ttl["key"].AsBsonDocument);
    }

    [Fact]
    public async Task Token_hashes_are_unique()
    {
        var repository = new MongoRefreshTokenRepository(await mongo.NewDatabaseAsync());
        await repository.AddAsync(NewToken(Guid.NewGuid(), "hash-1"), default);

        await Assert.ThrowsAsync<MongoWriteException>(() => repository.AddAsync(NewToken(Guid.NewGuid(), "hash-1"), default));
    }
}
