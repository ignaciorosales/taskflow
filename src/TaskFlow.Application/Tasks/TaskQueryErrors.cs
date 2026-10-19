using TaskFlow.Domain.Common;

namespace TaskFlow.Application.Tasks;

public static class TaskQueryErrors
{
    public static readonly Error PageInvalid = new("query.page_invalid", "Page must be 1 or greater.", "page");
    public static readonly Error PageSizeInvalid = new("query.page_size_invalid", "Page size must be between 1 and 100.", "pageSize");
    public static readonly Error StatusInvalid = new("query.status_invalid", "Status filter is not valid.", "status");
    public static readonly Error PriorityInvalid = new("query.priority_invalid", "Priority filter is not valid.", "priority");
    public static readonly Error SortByInvalid = new("query.sort_by_invalid", "Sort field is not valid.", "sortBy");
}
