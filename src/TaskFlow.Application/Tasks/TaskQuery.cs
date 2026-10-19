using TaskFlow.Domain.Tasks;

namespace TaskFlow.Application.Tasks;

public enum TaskSortBy
{
    CreatedAt,
    DueDate,
    Priority,
}

public sealed record TaskQuery(
    TaskItemStatus? Status = null,
    TaskPriority? Priority = null,
    bool OverdueOnly = false,
    TaskSortBy SortBy = TaskSortBy.CreatedAt,
    bool Descending = true,
    int Page = 1,
    int PageSize = 20)
{
    public const int MaxPageSize = 100;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);
