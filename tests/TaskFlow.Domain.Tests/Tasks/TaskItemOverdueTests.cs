using TaskFlow.Domain.Tasks;

namespace TaskFlow.Domain.Tests.Tasks;

public class TaskItemOverdueTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DueDate = CreatedAt.AddHours(1);

    private static TaskItem NewTask(DateTimeOffset? dueDate = null)
        => TaskItem.Create(Guid.NewGuid(), "Buy milk", null, null, dueDate, CreatedAt).Value;

    [Fact]
    public void Task_without_due_date_is_never_overdue()
    {
        Assert.False(NewTask().IsOverdue(CreatedAt.AddYears(1)));
    }

    [Theory]
    [InlineData(TaskItemStatus.Todo)]
    [InlineData(TaskItemStatus.InProgress)]
    public void Open_task_is_overdue_one_tick_after_its_due_date(TaskItemStatus status)
    {
        var task = NewTask(DueDate);
        task.ChangeStatus(status, CreatedAt);

        Assert.True(task.IsOverdue(DueDate.AddTicks(1)));
    }

    [Fact]
    public void Task_is_not_overdue_at_its_due_date()
    {
        Assert.False(NewTask(DueDate).IsOverdue(DueDate));
    }

    [Fact]
    public void Done_task_is_not_overdue()
    {
        var task = NewTask(DueDate);
        task.ChangeStatus(TaskItemStatus.Done, DueDate.AddHours(1));

        Assert.False(task.IsOverdue(DueDate.AddDays(1)));
    }

    [Fact]
    public void Task_completed_after_its_due_date_is_completed_late()
    {
        var task = NewTask(DueDate);
        task.ChangeStatus(TaskItemStatus.Done, DueDate.AddTicks(1));

        Assert.True(task.IsCompletedLate);
    }

    [Fact]
    public void Task_completed_at_its_due_date_is_not_completed_late()
    {
        var task = NewTask(DueDate);
        task.ChangeStatus(TaskItemStatus.Done, DueDate);

        Assert.False(task.IsCompletedLate);
    }

    [Fact]
    public void Task_completed_without_due_date_is_not_completed_late()
    {
        var task = NewTask();
        task.ChangeStatus(TaskItemStatus.Done, CreatedAt.AddYears(1));

        Assert.False(task.IsCompletedLate);
    }

    [Fact]
    public void Reopened_task_is_no_longer_completed_late()
    {
        var task = NewTask(DueDate);
        task.ChangeStatus(TaskItemStatus.Done, DueDate.AddHours(1));

        task.ChangeStatus(TaskItemStatus.InProgress, DueDate.AddHours(2));

        Assert.False(task.IsCompletedLate);
    }

    [Fact]
    public void Open_task_is_not_completed_late()
    {
        Assert.False(NewTask(DueDate).IsCompletedLate);
    }
}
