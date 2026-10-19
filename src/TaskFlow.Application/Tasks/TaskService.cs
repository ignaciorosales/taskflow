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

    public async Task<Result<TaskDto>> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var now = Now();
        var created = TaskItem.Create(userId, request.Title, request.Description, request.Priority, ToMilliseconds(request.DueDate), now);
        if (created.IsFailure)
            return Result<TaskDto>.Failure([.. created.Errors]);

        await _repository.AddAsync(created.Value, cancellationToken);
        return Result<TaskDto>.Success(ToDto(created.Value, now));
    }

    public async Task<Result<TaskDto>> GetAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await FindOwnedAsync(userId, taskId, cancellationToken);
        if (task is null)
            return Result<TaskDto>.Failure(TaskErrors.NotFound);

        return Result<TaskDto>.Success(ToDto(task, Now()));
    }

    public async Task<Result<PagedResult<TaskDto>>> ListAsync(Guid userId, TaskQuery query, CancellationToken cancellationToken)
    {
        var errors = Validate(query);
        if (errors.Count > 0)
            return Result<PagedResult<TaskDto>>.Failure([.. errors]);

        var now = Now();
        var page = await _repository.ListAsync(userId, query, now, cancellationToken);
        return Result<PagedResult<TaskDto>>.Success(
            new PagedResult<TaskDto>([.. page.Items.Select(t => ToDto(t, now))], page.Page, page.PageSize, page.TotalCount));
    }

    public async Task<Result<TaskDto>> UpdateAsync(Guid userId, Guid taskId, UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await FindOwnedAsync(userId, taskId, cancellationToken);
        if (task is null)
            return Result<TaskDto>.Failure(TaskErrors.NotFound);

        var now = Now();
        var edited = task.Edit(request.Title, request.Description, request.Priority, ToMilliseconds(request.DueDate), now);
        if (edited.IsFailure)
            return Result<TaskDto>.Failure([.. edited.Errors]);

        await _repository.UpdateAsync(task, cancellationToken);
        return Result<TaskDto>.Success(ToDto(task, now));
    }

    public async Task<Result<TaskDto>> ChangeStatusAsync(Guid userId, Guid taskId, TaskItemStatus? status, CancellationToken cancellationToken)
    {
        var task = await FindOwnedAsync(userId, taskId, cancellationToken);
        if (task is null)
            return Result<TaskDto>.Failure(TaskErrors.NotFound);

        var now = Now();
        var changed = task.ChangeStatus(status, now);
        if (changed.IsFailure)
            return Result<TaskDto>.Failure([.. changed.Errors]);

        await _repository.UpdateAsync(task, cancellationToken);
        return Result<TaskDto>.Success(ToDto(task, now));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await FindOwnedAsync(userId, taskId, cancellationToken);
        if (task is null)
            return Result.Failure(TaskErrors.NotFound);

        task.Delete(Now());
        await _repository.UpdateAsync(task, cancellationToken);
        return Result.Success();
    }

    // Mongo stores milliseconds; trimming up front means a task always reads back exactly as it was returned.
    private DateTimeOffset Now() => ToMilliseconds(_timeProvider.GetUtcNow())!.Value;

    private static DateTimeOffset? ToMilliseconds(DateTimeOffset? value)
        => value is { } v ? new DateTimeOffset(v.Ticks - v.Ticks % TimeSpan.TicksPerMillisecond, v.Offset) : null;

    // Someone else's task and a deleted one come back exactly like a missing one.
    private async Task<TaskItem?> FindOwnedAsync(Guid userId, Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _repository.GetByIdAsync(taskId, cancellationToken);
        return task is not null && task.OwnerId == userId && task.DeletedAt is null ? task : null;
    }

    private static List<Error> Validate(TaskQuery query)
    {
        var errors = new List<Error>();
        if (query.Page < 1)
            errors.Add(TaskQueryErrors.PageInvalid);
        if (query.PageSize is < 1 or > TaskQuery.MaxPageSize)
            errors.Add(TaskQueryErrors.PageSizeInvalid);
        if (query.Status is { } status && !Enum.IsDefined(status))
            errors.Add(TaskQueryErrors.StatusInvalid);
        if (query.Priority is { } priority && !Enum.IsDefined(priority))
            errors.Add(TaskQueryErrors.PriorityInvalid);
        if (!Enum.IsDefined(query.SortBy))
            errors.Add(TaskQueryErrors.SortByInvalid);
        return errors;
    }

    private static TaskDto ToDto(TaskItem task, DateTimeOffset now) => new(
        task.Id,
        task.Title,
        task.Description,
        task.Priority,
        task.DueDate,
        task.Status,
        task.CreatedAt,
        task.CompletedAt,
        task.IsOverdue(now),
        task.IsCompletedLate);
}
