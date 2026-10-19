using Microsoft.Extensions.Time.Testing;
using MongoDB.Driver;
using TaskFlow.Application.Auth;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Tasks;
using TaskFlow.Domain.Users;
using TaskFlow.Infrastructure.Persistence;
using TaskFlow.Infrastructure.Security;

namespace TaskFlow.Infrastructure.Tests.Persistence;

public class DemoSeederTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    // Far from the real clock, so passing proves every demo date is relative to setup time.
    private static readonly DateTimeOffset SetupTime = new(2031, 3, 10, 8, 0, 0, TimeSpan.Zero);

    private readonly AspNetPasswordHasher _hasher = new();

    private async Task<(DemoSeeder Seeder, MongoUserRepository Users, MongoTaskRepository Tasks)> ArrangeAsync()
    {
        var database = await mongo.NewDatabaseAsync();
        var users = new MongoUserRepository(database);
        var tasks = new MongoTaskRepository(database);
        return (new DemoSeeder(users, tasks, _hasher, new FakeTimeProvider(SetupTime)), users, tasks);
    }

    private static Email DemoEmail => Email.Create(DemoSeeder.Email).Value;

    private static async Task<IReadOnlyList<TaskItem>> DemoTasksAsync(MongoTaskRepository tasks, Guid ownerId)
        => (await tasks.ListAsync(ownerId, new TaskQuery(PageSize: TaskQuery.MaxPageSize), SetupTime, default)).Items;

    [Fact]
    public void Demo_password_meets_the_password_rules()
    {
        Assert.Empty(PasswordPolicy.Validate(DemoSeeder.Password));
    }

    [Fact]
    public async Task Seeding_creates_a_demo_account_that_logs_in_with_the_published_password()
    {
        var (seeder, users, _) = await ArrangeAsync();

        await seeder.SeedAsync();

        var demo = await users.GetByEmailAsync(DemoEmail, default);
        Assert.NotNull(demo);
        Assert.True(_hasher.Verify(demo.PasswordHash, DemoSeeder.Password));
    }

    [Fact]
    public async Task Demo_has_tasks_in_every_status_including_an_overdue_one()
    {
        var (seeder, users, tasks) = await ArrangeAsync();

        await seeder.SeedAsync();
        var demoTasks = await DemoTasksAsync(tasks, (await users.GetByEmailAsync(DemoEmail, default))!.Id);

        Assert.Equal(Enum.GetValues<TaskItemStatus>(), demoTasks.Select(t => t.Status).Distinct().Order());
        Assert.Contains(demoTasks, t => t.IsOverdue(SetupTime));
        Assert.Contains(demoTasks, t => t.IsCompletedLate);
        Assert.Contains(demoTasks, t => t.DueDate is null);
        Assert.Equal(Enum.GetValues<TaskPriority>(), demoTasks.Select(t => t.Priority).Distinct().Order());
    }

    [Fact]
    public async Task Seeding_again_changes_nothing()
    {
        var (seeder, users, tasks) = await ArrangeAsync();
        await seeder.SeedAsync();
        var demo = (await users.GetByEmailAsync(DemoEmail, default))!;
        var before = (await DemoTasksAsync(tasks, demo.Id)).Select(t => t.Id).Order().ToList();

        await seeder.SeedAsync();

        Assert.Equal(demo.Id, (await users.GetByEmailAsync(DemoEmail, default))!.Id);
        Assert.Equal(before, (await DemoTasksAsync(tasks, demo.Id)).Select(t => t.Id).Order());
    }

    [Fact]
    public async Task Existing_demo_account_is_left_alone_even_if_its_tasks_were_changed()
    {
        var (seeder, users, tasks) = await ArrangeAsync();
        await seeder.SeedAsync();
        var demo = (await users.GetByEmailAsync(DemoEmail, default))!;
        foreach (var task in await DemoTasksAsync(tasks, demo.Id))
        {
            task.Delete(SetupTime);
            await tasks.UpdateAsync(task, default);
        }

        await seeder.SeedAsync();

        Assert.Empty(await DemoTasksAsync(tasks, demo.Id));
    }
}
