using TaskFlow.Domain.Tasks;

namespace TaskFlow.Application.Tasks;

public interface ITaskRepository
{
    /// <summary>The owner's task if it isn't soft-deleted; someone else's or a deleted one comes back as null.</summary>
    Task<TaskItem?> GetByIdAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Returns a page of the owner's tasks that aren't soft-deleted, filtered and sorted as asked.</summary>
    Task<PagedResult<TaskItem>> ListAsync(Guid ownerId, TaskQuery query, DateTimeOffset now, CancellationToken cancellationToken);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken);

    /// <summary>Saves the task only while it's still its owner's and not deleted; false means nothing was saved.</summary>
    Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken);
}
