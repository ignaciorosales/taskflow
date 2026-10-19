using TaskFlow.Domain.Tasks;

namespace TaskFlow.Application.Tasks;

public interface ITaskRepository
{
    /// <summary>Returns the task whoever owns it, including soft-deleted ones; ownership is the service's call.</summary>
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Returns a page of the owner's tasks that aren't soft-deleted, filtered and sorted as asked.</summary>
    Task<PagedResult<TaskItem>> ListAsync(Guid ownerId, TaskQuery query, DateTimeOffset now, CancellationToken cancellationToken);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken);

    Task UpdateAsync(TaskItem task, CancellationToken cancellationToken);
}
