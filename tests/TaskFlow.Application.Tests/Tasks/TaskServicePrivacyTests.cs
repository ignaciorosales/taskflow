using NSubstitute;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Tasks;

namespace TaskFlow.Application.Tests.Tasks;

public class TaskServicePrivacyTests : TaskServiceTestBase
{
    public enum Target
    {
        Missing,
        SomeoneElses,
        Deleted,
    }

    private static readonly Dictionary<string, Func<TaskService, Guid, Guid, Task<Result>>> Operations = new()
    {
        ["get"] = async (service, userId, taskId) => await service.GetAsync(userId, taskId, default),
        ["update"] = async (service, userId, taskId) =>
            await service.UpdateAsync(userId, taskId, new("Changed", null, TaskPriority.High, null), default),
        ["change status"] = async (service, userId, taskId) =>
            await service.ChangeStatusAsync(userId, taskId, TaskItemStatus.Done, default),
        ["delete"] = async (service, userId, taskId) => await service.DeleteAsync(userId, taskId, default),
    };

    public static TheoryData<Target, string> Cases()
    {
        var data = new TheoryData<Target, string>();
        foreach (var target in Enum.GetValues<Target>())
            foreach (var operation in Operations.Keys)
                data.Add(target, operation);
        return data;
    }

    private Guid IdOf(Target target)
    {
        switch (target)
        {
            case Target.Missing:
                return Guid.NewGuid();
            case Target.SomeoneElses:
                return StoredTask(OtherUserId).Id;
            default:
                var task = StoredTask(UserId);
                task.Delete(Now);
                return task.Id;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Someone_elses_or_deleted_task_looks_exactly_like_a_missing_one(Target target, string operation)
    {
        var result = await Operations[operation](Service, UserId, IdOf(target));

        Assert.Equal([TaskErrors.NotFound], result.Errors);
        await Repository.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("update")]
    [InlineData("change status")]
    [InlineData("delete")]
    public async Task Someone_elses_task_is_left_untouched(string operation)
    {
        var task = StoredTask(OtherUserId);

        await Operations[operation](Service, UserId, task.Id);

        Assert.Equal("Buy milk", task.Title);
        Assert.Equal(TaskPriority.Low, task.Priority);
        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.Null(task.DeletedAt);
    }

    [Fact]
    public async Task Asks_the_repository_only_for_the_callers_task()
    {
        var task = StoredTask(UserId);

        await Service.GetAsync(UserId, task.Id, default);

        await Repository.Received(1).GetByIdAsync(UserId, task.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_can_reach_their_own_task()
    {
        var task = StoredTask(UserId);

        Assert.True((await Service.GetAsync(UserId, task.Id, default)).IsSuccess);
    }
}
