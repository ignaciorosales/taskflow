using TaskFlow.Application.Auth;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Tasks;
using TaskFlow.Domain.Users;
using EmailAddress = TaskFlow.Domain.Users.Email;

namespace TaskFlow.Infrastructure.Persistence;

/// <summary>Creates the demo account and its tasks once; does nothing if the account already exists.</summary>
public sealed class DemoSeeder
{
    public const string Email = "demo@taskflow.dev";
    public const string Password = "Demo123!";

    private readonly IUserRepository _users;
    private readonly ITaskRepository _tasks;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public DemoSeeder(IUserRepository users, ITaskRepository tasks, IPasswordHasher passwordHasher, TimeProvider timeProvider)
    {
        _users = users;
        _tasks = tasks;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = EmailAddress.Create(Email).Value;
        if (await _users.GetByEmailAsync(email, cancellationToken) is not null)
            return;

        var now = _timeProvider.GetUtcNow();
        var user = User.Create(email, _passwordHasher.Hash(Password), now);
        // Known limit: a crash between the user and its tasks leaves a demo with no tasks; drop the database to reseed.
        if ((await _users.AddAsync(user, cancellationToken)).IsFailure)
            return; // another instance seeded it first

        foreach (var task in DemoTasks(user.Id, now))
            await _tasks.AddAsync(task, cancellationToken);
    }

    // Created in the past through the normal rules, so the overdue and late tasks need no special path.
    private static IEnumerable<TaskItem> DemoTasks(Guid owner, DateTimeOffset now)
    {
        yield return Task(owner, "Prepare the quarterly report", "Numbers from finance, slides for Friday.",
            TaskPriority.High, createdAt: now.AddDays(-2), dueDate: now.AddDays(2));
        yield return Task(owner, "Book a dentist appointment", null,
            TaskPriority.Low, createdAt: now.AddHours(-6), dueDate: null);
        yield return Task(owner, "Review open pull requests", "Two from the API team are waiting.",
            TaskPriority.Medium, createdAt: now.AddDays(-1), dueDate: now.AddDays(1), TaskItemStatus.InProgress, now.AddHours(-3));
        yield return Task(owner, "Renew passport", "It expires next month and the form needs a new photo.",
            TaskPriority.High, createdAt: now.AddDays(-5), dueDate: now.AddDays(-1));
        yield return Task(owner, "Plan the weekend trip", null,
            TaskPriority.Low, createdAt: now.AddDays(-3), dueDate: now.AddDays(5), TaskItemStatus.InProgress, now.AddDays(-1));
        yield return Task(owner, "Pay the electricity bill", null,
            TaskPriority.Medium, createdAt: now.AddDays(-7), dueDate: now.AddDays(-2), TaskItemStatus.Done, now.AddDays(-3));
        yield return Task(owner, "Send tax documents to the accountant", null,
            TaskPriority.High, createdAt: now.AddDays(-10), dueDate: now.AddDays(-5), TaskItemStatus.Done, now.AddDays(-4));
    }

    private static TaskItem Task(
        Guid owner, string title, string? description, TaskPriority priority, DateTimeOffset createdAt, DateTimeOffset? dueDate,
        TaskItemStatus status = TaskItemStatus.Todo, DateTimeOffset? statusChangedAt = null)
    {
        var task = TaskItem.Create(owner, title, description, priority, dueDate, createdAt).Value;
        if (status != TaskItemStatus.Todo)
            task.ChangeStatus(status, statusChangedAt ?? createdAt);
        return task;
    }
}
