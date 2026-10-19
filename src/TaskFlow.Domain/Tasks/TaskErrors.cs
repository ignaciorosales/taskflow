using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Tasks;

public static class TaskErrors
{
    public static readonly Error TitleRequired = new("task.title_required", "Title is required.", "title");
    public static readonly Error TitleTooLong = new("task.title_too_long", "Title can't be longer than 100 characters.", "title");
    public static readonly Error DescriptionTooLong = new("task.description_too_long", "Description can't be longer than 1000 characters.", "description");
    public static readonly Error DueDateInPast = new("task.due_date_in_past", "Due date can't be in the past.", "dueDate");
    public static readonly Error DueDateCannotBeRemoved = new("task.due_date_cannot_be_removed", "Due date can't be removed once set.", "dueDate");
    public static readonly Error PriorityInvalid = new("task.priority_invalid", "Priority is not valid.", "priority");
    public static readonly Error StatusInvalid = new("task.status_invalid", "Status is not valid.", "status");
}
