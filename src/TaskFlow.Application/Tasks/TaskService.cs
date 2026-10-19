using TaskFlow.Domain.Common;
using TaskFlow.Domain.Tasks;

namespace TaskFlow.Application.Tasks;

public sealed class TaskService
{
    private readonly ITaskRepository _repository;
    private readonly TimeProvider _timeProvider;

    public TaskService(ITaskRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public Task<Result<TaskDto>> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<Result<TaskDto>> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<Result<PagedResult<TaskDto>>> ListAsync(Guid userId, TaskQuery query, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<Result<TaskDto>> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<Result<TaskDto>> ChangeStatusAsync(Guid userId, Guid taskId, TaskItemStatus status, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task<Result> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
