using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Tasks;

public sealed class TaskItem
{
    public const int TitleMaxLength = 100;
    public const int DescriptionMaxLength = 1000;

    private TaskItem() { }

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

    public bool IsCompletedLate => CompletedAt > DueDate;

    public static Result<TaskItem> Create(
        Guid ownerId, string? title, string? description, TaskPriority? priority, DateTimeOffset? dueDate, DateTimeOffset now)
    {
        var errors = ValidateFields(title, description, priority ?? TaskPriority.Medium);
        if (dueDate < now)
            errors.Add(TaskErrors.DueDateInPast);

        if (errors.Count > 0)
            return Result<TaskItem>.Failure([.. errors]);

        return Result<TaskItem>.Success(new TaskItem
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = title!.Trim(),
            Description = Clean(description),
            Priority = priority ?? TaskPriority.Medium,
            DueDate = dueDate?.ToUniversalTime(),
            Status = TaskItemStatus.Todo,
            CreatedAt = now,
        });
    }

    public Result Edit(string? title, string? description, TaskPriority priority, DateTimeOffset? dueDate, DateTimeOffset now)
    {
        var errors = ValidateFields(title, description, priority);
        if (DueDate is not null && dueDate is null)
            errors.Add(TaskErrors.DueDateCannotBeRemoved);
        else if (dueDate != DueDate && dueDate < now)
            errors.Add(TaskErrors.DueDateInPast);

        if (errors.Count > 0)
            return Result.Failure([.. errors]);

        Title = title!.Trim();
        Description = Clean(description);
        Priority = priority;
        DueDate = dueDate?.ToUniversalTime();
        return Result.Success();
    }

    public Result ChangeStatus(TaskItemStatus status, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status))
            return Result.Failure(TaskErrors.StatusInvalid);

        if (status != TaskItemStatus.Done)
            CompletedAt = null;
        else if (Status != TaskItemStatus.Done)
            CompletedAt = now;

        Status = status;
        return Result.Success();
    }

    public bool IsOverdue(DateTimeOffset now) => Status != TaskItemStatus.Done && DueDate < now;

    public void Delete(DateTimeOffset now) => DeletedAt = now;

    private static List<Error> ValidateFields(string? title, string? description, TaskPriority priority)
    {
        var errors = new List<Error>();
        if (string.IsNullOrWhiteSpace(title))
            errors.Add(TaskErrors.TitleRequired);
        else if (title.Trim().Length > TitleMaxLength)
            errors.Add(TaskErrors.TitleTooLong);

        if (description?.Trim().Length > DescriptionMaxLength)
            errors.Add(TaskErrors.DescriptionTooLong);

        if (!Enum.IsDefined(priority))
            errors.Add(TaskErrors.PriorityInvalid);

        return errors;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
