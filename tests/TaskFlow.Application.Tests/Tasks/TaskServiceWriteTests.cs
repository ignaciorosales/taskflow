using NSubstitute;
using TaskFlow.Domain.Tasks;

namespace TaskFlow.Application.Tests.Tasks;

public class TaskServiceWriteTests : TaskServiceTestBase
{
    [Fact]
    public async Task Update_edits_the_callers_task_and_saves_it()
    {
        var task = StoredTask(UserId);

        var result = await Service.UpdateAsync(UserId, task.Id, new("Buy bread", null, TaskPriority.High, Now.AddDays(2)), default);

        Assert.Equal("Buy bread", result.Value.Title);
        Assert.Equal(TaskPriority.High, result.Value.Priority);
        await Repository.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Overdue_task_can_be_updated_keeping_its_past_due_date()
    {
        var task = StoredTask(UserId, Now.AddHours(1));
        Time.Advance(TimeSpan.FromHours(2));

        var result = await Service.UpdateAsync(UserId, task.Id, new("Buy bread", null, TaskPriority.Low, Now.AddHours(1)), default);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsOverdue);
    }

    [Fact]
    public async Task Invalid_update_returns_the_errors_and_saves_nothing()
    {
        var task = StoredTask(UserId, Now.AddHours(1));

        var result = await Service.UpdateAsync(UserId, task.Id, new(" ", null, TaskPriority.Low, null), default);

        Assert.Equal([TaskErrors.TitleRequired, TaskErrors.DueDateCannotBeRemoved], result.Errors);
        await Repository.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Status_change_is_saved_with_the_completion_time_from_the_clock()
    {
        var task = StoredTask(UserId);
        Time.Advance(TimeSpan.FromHours(1));

        var result = await Service.ChangeStatusAsync(UserId, task.Id, TaskItemStatus.Done, default);

        Assert.Equal(TaskItemStatus.Done, result.Value.Status);
        Assert.Equal(Now.AddHours(1), result.Value.CompletedAt);
        await Repository.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Invalid_status_is_rejected_and_nothing_is_saved()
    {
        var task = StoredTask(UserId);

        var result = await Service.ChangeStatusAsync(UserId, task.Id, (TaskItemStatus)42, default);

        Assert.Equal([TaskErrors.StatusInvalid], result.Errors);
        await Repository.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_soft_deletes_the_callers_task()
    {
        var task = StoredTask(UserId);
        Time.Advance(TimeSpan.FromMinutes(5));

        var result = await Service.DeleteAsync(UserId, task.Id, default);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now.AddMinutes(5), task.DeletedAt);
        await Repository.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }
}
