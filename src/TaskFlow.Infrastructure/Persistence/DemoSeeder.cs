using TaskFlow.Application.Auth;
using TaskFlow.Application.Tasks;

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

    public Task SeedAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
