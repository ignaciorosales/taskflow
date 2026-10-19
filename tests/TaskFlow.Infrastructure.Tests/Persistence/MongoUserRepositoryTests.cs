using MongoDB.Bson;
using MongoDB.Driver;
using TaskFlow.Domain.Users;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Tests.Persistence;

public class MongoUserRepositoryTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static User NewUser(string email) => User.Create(Email.Create(email).Value, "hashed-password", Now);

    [Fact]
    public async Task Added_user_can_be_found_by_email_and_by_id()
    {
        var repository = new MongoUserRepository(await mongo.NewDatabaseAsync());
        var user = NewUser("john@example.com");

        Assert.True((await repository.AddAsync(user, default)).IsSuccess);

        foreach (var found in new[]
        {
            await repository.GetByEmailAsync(Email.Create("john@example.com").Value, default),
            await repository.GetByIdAsync(user.Id, default),
        })
        {
            Assert.NotNull(found);
            Assert.Equal(user.Id, found.Id);
            Assert.Equal(user.Email, found.Email);
            Assert.Equal("hashed-password", found.PasswordHash);
            Assert.Equal(Now, found.CreatedAt);
        }
    }

    [Fact]
    public async Task Unknown_email_or_id_finds_nothing()
    {
        var repository = new MongoUserRepository(await mongo.NewDatabaseAsync());
        await repository.AddAsync(NewUser("john@example.com"), default);

        Assert.Null(await repository.GetByEmailAsync(Email.Create("jane@example.com").Value, default));
        Assert.Null(await repository.GetByIdAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Registering_an_email_twice_returns_EmailTaken_instead_of_throwing()
    {
        var repository = new MongoUserRepository(await mongo.NewDatabaseAsync());
        var first = NewUser("john@example.com");
        await repository.AddAsync(first, default);

        var result = await repository.AddAsync(NewUser("  JOHN@Example.com "), default);

        Assert.Equal([UserErrors.EmailTaken], result.Errors);
        Assert.Equal(first.Id, (await repository.GetByEmailAsync(first.Email, default))!.Id);
    }

    [Fact]
    public async Task Only_one_of_many_concurrent_registrations_of_an_email_succeeds()
    {
        var database = await mongo.NewDatabaseAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => new MongoUserRepository(database).AddAsync(NewUser("john@example.com"), default)));

        Assert.Single(results, r => r.IsSuccess);
        Assert.All(results.Where(r => r.IsFailure), r => Assert.Equal([UserErrors.EmailTaken], r.Errors));
    }

    [Fact]
    public async Task Email_uniqueness_is_enforced_by_a_unique_index()
    {
        var database = await mongo.NewDatabaseAsync();

        var indexes = await (await database.GetCollection<BsonDocument>(MongoSetup.UsersCollection)
            .Indexes.ListAsync()).ToListAsync();

        Assert.Contains(indexes, index => index.GetValue("unique", false).AsBoolean);
    }

    [Fact]
    public async Task Configuring_twice_is_harmless()
    {
        var database = await mongo.NewDatabaseAsync();
        await MongoSetup.ConfigureAsync(database);
        var repository = new MongoUserRepository(database);

        await repository.AddAsync(NewUser("john@example.com"), default);

        Assert.Equal([UserErrors.EmailTaken], (await repository.AddAsync(NewUser("john@example.com"), default)).Errors);
    }
}
