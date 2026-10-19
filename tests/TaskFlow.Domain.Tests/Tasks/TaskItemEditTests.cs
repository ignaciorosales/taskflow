using TaskFlow.Domain.Tasks;

namespace TaskFlow.Domain.Tests.Tasks;

public class TaskItemEditTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OriginalDueDate = CreatedAt.AddHours(1);
    private static readonly DateTimeOffset Later = CreatedAt.AddHours(2);

    private static TaskItem NewTask(DateTimeOffset? dueDate = null)
        => TaskItem.Create(Guid.NewGuid(), "Buy milk", "Semi-skimmed", TaskPriority.Low, dueDate, CreatedAt).Value;

    [Fact]
    public void Edit_updates_the_fields_trimmed()
    {
        var task = NewTask();

        var result = task.Edit("  Buy bread ", "  Wholegrain ", TaskPriority.High, Later.AddDays(1), Later);

        Assert.True(result.IsSuccess);
        Assert.Equal("Buy bread", task.Title);
        Assert.Equal("Wholegrain", task.Description);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(Later.AddDays(1), task.DueDate);
    }

    [Fact]
    public void Invalid_edit_reports_every_error_and_leaves_the_task_unchanged()
    {
        var task = NewTask(OriginalDueDate);

        var result = task.Edit(" ", new string('d', 1001), (TaskPriority)42, CreatedAt.AddTicks(-1), CreatedAt);

        Assert.Equal(
            [TaskErrors.TitleRequired, TaskErrors.DescriptionTooLong, TaskErrors.PriorityInvalid, TaskErrors.DueDateInPast],
            result.Errors);
        Assert.Equal("Buy milk", task.Title);
        Assert.Equal("Semi-skimmed", task.Description);
        Assert.Equal(TaskPriority.Low, task.Priority);
        Assert.Equal(OriginalDueDate, task.DueDate);
    }

    [Fact]
    public void Title_of_101_characters_is_rejected()
    {
        var result = NewTask().Edit(new string('a', 101), null, TaskPriority.Low, null, Later);

        Assert.Equal([TaskErrors.TitleTooLong], result.Errors);
    }

    [Fact]
    public void Overdue_task_can_be_edited_keeping_its_past_due_date()
    {
        var task = NewTask(OriginalDueDate);

        var result = task.Edit("Buy bread", null, TaskPriority.Low, OriginalDueDate, Later);

        Assert.True(result.IsSuccess);
        Assert.Equal("Buy bread", task.Title);
        Assert.Equal(OriginalDueDate, task.DueDate);
    }

    [Fact]
    public void Same_due_date_in_another_offset_counts_as_unchanged()
    {
        var task = NewTask(OriginalDueDate);

        var result = task.Edit("Buy milk", null, TaskPriority.Low, OriginalDueDate.ToOffset(TimeSpan.FromHours(-5)), Later);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.Zero, task.DueDate!.Value.Offset);
    }

    [Fact]
    public void Due_date_cannot_be_moved_into_the_past()
    {
        var task = NewTask(OriginalDueDate);

        var result = task.Edit("Buy milk", null, TaskPriority.Low, Later.AddTicks(-1), Later);

        Assert.Equal([TaskErrors.DueDateInPast], result.Errors);
        Assert.Equal(OriginalDueDate, task.DueDate);
    }

    [Fact]
    public void Due_date_can_be_moved_to_now()
    {
        var task = NewTask(OriginalDueDate);

        Assert.True(task.Edit("Buy milk", null, TaskPriority.Low, Later, Later).IsSuccess);
        Assert.Equal(Later, task.DueDate);
    }

    [Fact]
    public void Due_date_cannot_be_removed_once_set()
    {
        var task = NewTask(OriginalDueDate);

        var result = task.Edit("Buy milk", null, TaskPriority.Low, null, Later);

        Assert.Equal([TaskErrors.DueDateCannotBeRemoved], result.Errors);
        Assert.Equal(OriginalDueDate, task.DueDate);
    }

    [Fact]
    public void Task_without_due_date_can_stay_without_one()
    {
        var task = NewTask();

        Assert.True(task.Edit("Buy milk", null, TaskPriority.Low, null, Later).IsSuccess);
        Assert.Null(task.DueDate);
    }

    [Fact]
    public void Due_date_can_be_added_later()
    {
        var task = NewTask();

        Assert.True(task.Edit("Buy milk", null, TaskPriority.Low, Later.AddDays(1), Later).IsSuccess);
        Assert.Equal(Later.AddDays(1), task.DueDate);
    }

    [Fact]
    public void Edit_does_not_touch_the_status()
    {
        var task = NewTask();
        task.ChangeStatus(TaskItemStatus.InProgress, CreatedAt);

        task.Edit("Buy bread", null, TaskPriority.Low, null, Later);

        Assert.Equal(TaskItemStatus.InProgress, task.Status);
    }
}
