using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Tasks;

namespace TaskFlow.Application.Tests.Tasks;

public abstract class TaskServiceTestBase
{
    // Later than the real clock, so a test only passes if the service reads time from TimeProvider.
    protected static readonly DateTimeOffset Now = new(2030, 6, 1, 9, 0, 0, TimeSpan.Zero);

    protected readonly Guid UserId = Guid.NewGuid();
    protected readonly Guid OtherUserId = Guid.NewGuid();
    protected readonly ITaskRepository Repository = Substitute.For<ITaskRepository>();
    protected readonly FakeTimeProvider Time = new(Now);
    protected readonly TaskService Service;

    protected TaskServiceTestBase() => Service = new TaskService(Repository, Time);

    protected TaskItem StoredTask(Guid ownerId, DateTimeOffset? dueDate = null)
    {
        var task = TaskItem.Create(ownerId, "Buy milk", "Semi-skimmed", TaskPriority.Low, dueDate, Now).Value;
        Repository.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);
        return task;
    }
}
