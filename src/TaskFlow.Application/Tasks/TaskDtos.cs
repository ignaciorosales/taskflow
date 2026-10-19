using TaskFlow.Domain.Tasks;

namespace TaskFlow.Application.Tasks;

public sealed record CreateTaskRequest(string? Title, string? Description, TaskPriority? Priority, DateTimeOffset? DueDate);

public sealed record UpdateTaskRequest(string? Title, string? Description, TaskPriority? Priority, DateTimeOffset? DueDate);

public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    TaskPriority Priority,
    DateTimeOffset? DueDate,
    TaskItemStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    bool IsOverdue,
    bool IsCompletedLate);
