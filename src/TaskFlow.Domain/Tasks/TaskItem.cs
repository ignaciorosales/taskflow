using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Tasks;

public sealed class TaskItem
{
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public TaskItemStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    public bool IsCompletedLate => throw new NotImplementedException();

    public static Result<TaskItem> Create(
        Guid ownerId, string? title, string? description, TaskPriority? priority, DateTimeOffset? dueDate, DateTimeOffset now)
        => throw new NotImplementedException();

    public Result Edit(string? title, string? description, TaskPriority priority, DateTimeOffset? dueDate, DateTimeOffset now)
        => throw new NotImplementedException();

    public Result ChangeStatus(TaskItemStatus status, DateTimeOffset now) => throw new NotImplementedException();

    public bool IsOverdue(DateTimeOffset now) => throw new NotImplementedException();

    public void Delete(DateTimeOffset now) => throw new NotImplementedException();
}
