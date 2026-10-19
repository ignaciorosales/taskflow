using TaskFlow.Domain.Common;
using TaskFlow.Domain.Tasks;

namespace TaskFlow.Domain.Tests.Tasks;

public class TaskItemCreateTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static Result<TaskItem> Create(
        string? title = "Buy milk", string? description = null, TaskPriority? priority = null, DateTimeOffset? dueDate = null)
        => TaskItem.Create(OwnerId, title, description, priority, dueDate, Now);

    [Fact]
    public void New_task_starts_as_todo_with_medium_priority()
    {
        var task = Create().Value;

        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(OwnerId, task.OwnerId);
        Assert.Equal(TaskItemStatus.Todo, task.Status);
        Assert.Equal(TaskPriority.Medium, task.Priority);
        Assert.Equal(Now, task.CreatedAt);
        Assert.Null(task.DueDate);
        Assert.Null(task.CompletedAt);
        Assert.Null(task.DeletedAt);
    }

    [Fact]
    public void Given_priority_is_kept()
    {
        Assert.Equal(TaskPriority.High, Create(priority: TaskPriority.High).Value.Priority);
    }

    [Fact]
    public void Undefined_priority_is_rejected()
    {
        Assert.Equal([TaskErrors.PriorityInvalid], Create(priority: (TaskPriority)42).Errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Blank_title_is_rejected(string? title)
    {
        var result = Create(title: title);

        Assert.True(result.IsFailure);
        Assert.Equal([TaskErrors.TitleRequired], result.Errors);
    }

    [Fact]
    public void Title_is_trimmed()
    {
        Assert.Equal("Buy milk", Create(title: "  Buy milk \t").Value.Title);
    }

    [Fact]
    public void Title_of_100_characters_after_trimming_is_accepted()
    {
        var title = new string('a', 100);

        Assert.Equal(title, Create(title: $"  {title}  ").Value.Title);
    }

    [Fact]
    public void Title_of_101_characters_is_rejected()
    {
        Assert.Equal([TaskErrors.TitleTooLong], Create(title: new string('a', 101)).Errors);
    }

    [Fact]
    public void Description_is_trimmed()
    {
        Assert.Equal("Semi-skimmed", Create(description: "  Semi-skimmed  ").Value.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_description_becomes_no_description(string? description)
    {
        Assert.Null(Create(description: description).Value.Description);
    }

    [Fact]
    public void Description_of_1000_characters_after_trimming_is_accepted()
    {
        var description = new string('d', 1000);

        Assert.Equal(description, Create(description: $" {description} ").Value.Description);
    }

    [Fact]
    public void Description_of_1001_characters_is_rejected()
    {
        Assert.Equal([TaskErrors.DescriptionTooLong], Create(description: new string('d', 1001)).Errors);
    }

    [Fact]
    public void Due_date_one_tick_before_now_is_rejected()
    {
        Assert.Equal([TaskErrors.DueDateInPast], Create(dueDate: Now.AddTicks(-1)).Errors);
    }

    [Fact]
    public void Due_date_equal_to_now_is_accepted()
    {
        Assert.Equal(Now, Create(dueDate: Now).Value.DueDate);
    }

    [Fact]
    public void Due_date_is_compared_as_an_instant_not_as_local_clock_time()
    {
        // 13:00 at +02:00 is 11:00 UTC: an hour before Now, even though 13:00 reads later than 12:00.
        var dueDate = new DateTimeOffset(2026, 1, 15, 13, 0, 0, TimeSpan.FromHours(2));

        Assert.Equal([TaskErrors.DueDateInPast], Create(dueDate: dueDate).Errors);
    }

    [Fact]
    public void Due_date_is_stored_in_utc()
    {
        var dueDate = new DateTimeOffset(2026, 1, 15, 18, 0, 0, TimeSpan.FromHours(2));

        var stored = Create(dueDate: dueDate).Value.DueDate!.Value;

        Assert.Equal(TimeSpan.Zero, stored.Offset);
        Assert.Equal(dueDate.UtcDateTime, stored.UtcDateTime);
    }

    [Fact]
    public void Every_invalid_field_is_reported()
    {
        var result = Create(title: " ", description: new string('d', 1001), dueDate: Now.AddDays(-1));

        Assert.Equal(
            [TaskErrors.TitleRequired, TaskErrors.DescriptionTooLong, TaskErrors.DueDateInPast],
            result.Errors);
    }
}
