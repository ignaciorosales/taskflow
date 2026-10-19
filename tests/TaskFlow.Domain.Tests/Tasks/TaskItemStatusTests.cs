using TaskFlow.Domain.Tasks;

namespace TaskFlow.Domain.Tests.Tasks;

public class TaskItemStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    private static TaskItem NewTask()
        => TaskItem.Create(Guid.NewGuid(), "Buy milk", null, null, null, Now).Value;

    private static TaskItem TaskIn(TaskItemStatus status)
    {
        var task = NewTask();
        task.ChangeStatus(status, Now);
        return task;
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo, TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.Todo, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.Done, TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done, TaskItemStatus.Todo)]
    public void Any_move_between_statuses_is_allowed(TaskItemStatus from, TaskItemStatus to)
    {
        var task = TaskIn(from);

        var result = task.ChangeStatus(to, Now.AddHours(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(to, task.Status);
    }

    [Fact]
    public void Undefined_status_is_rejected_and_the_status_is_kept()
    {
        var task = TaskIn(TaskItemStatus.InProgress);

        var result = task.ChangeStatus((TaskItemStatus)42, Now);

        Assert.Equal([TaskErrors.StatusInvalid], result.Errors);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
    }

    [Fact]
    public void Becoming_done_records_the_completion_time()
    {
        var task = NewTask();

        task.ChangeStatus(TaskItemStatus.Done, Now.AddHours(3));

        Assert.Equal(Now.AddHours(3), task.CompletedAt);
    }

    [Theory]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Todo)]
    public void Leaving_done_clears_the_completion_time(TaskItemStatus to)
    {
        var task = TaskIn(TaskItemStatus.Done);

        task.ChangeStatus(to, Now.AddHours(1));

        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Marking_a_done_task_done_again_keeps_the_original_completion_time()
    {
        var task = TaskIn(TaskItemStatus.Done);

        task.ChangeStatus(TaskItemStatus.Done, Now.AddHours(1));

        Assert.Equal(Now, task.CompletedAt);
    }

    [Fact]
    public void Moving_between_open_statuses_leaves_no_completion_time()
    {
        var task = TaskIn(TaskItemStatus.InProgress);

        task.ChangeStatus(TaskItemStatus.Todo, Now);

        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public void Delete_records_when_the_task_was_deleted()
    {
        var task = NewTask();

        task.Delete(Now.AddHours(1));

        Assert.Equal(Now.AddHours(1), task.DeletedAt);
    }
}
