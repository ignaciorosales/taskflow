using TaskFlow.Application.Auth;
using TaskFlow.Infrastructure.Persistence;

namespace TaskFlow.Infrastructure.Tests.Persistence;

public class MongoLoginAttemptRepositoryTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2030, 6, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Email_without_failures_has_no_attempt()
    {
        var repository = new MongoLoginAttemptRepository(await mongo.NewDatabaseAsync());

        Assert.Null(await repository.GetAsync("john@example.com", default));
    }

    [Fact]
    public async Task Saving_twice_keeps_only_the_latest_attempt()
    {
        var repository = new MongoLoginAttemptRepository(await mongo.NewDatabaseAsync());

        await repository.SaveAsync(new("john@example.com", 1, null), default);
        await repository.SaveAsync(new("john@example.com", 0, Now.AddMinutes(1)), default);

        Assert.Equal(new LoginAttempt("john@example.com", 0, Now.AddMinutes(1)), await repository.GetAsync("john@example.com", default));
    }

    [Fact]
    public async Task Reset_forgets_the_attempts_of_that_email_only()
    {
        var repository = new MongoLoginAttemptRepository(await mongo.NewDatabaseAsync());
        await repository.SaveAsync(new("john@example.com", 2, null), default);
        await repository.SaveAsync(new("jane@example.com", 1, null), default);

        await repository.ResetAsync("john@example.com", default);

        Assert.Null(await repository.GetAsync("john@example.com", default));
        Assert.NotNull(await repository.GetAsync("jane@example.com", default));
    }
}
