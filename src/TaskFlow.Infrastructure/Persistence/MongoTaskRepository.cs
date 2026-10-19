using MongoDB.Driver;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Tasks;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class MongoTaskRepository : ITaskRepository
{
    private readonly IMongoCollection<TaskItem> _tasks;

    public MongoTaskRepository(IMongoDatabase database) => _tasks = database.GetCollection<TaskItem>(MongoSetup.TasksCollection);

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<PagedResult<TaskItem>> ListAsync(Guid ownerId, TaskQuery query, DateTimeOffset now, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public Task AddAsync(TaskItem task, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken) => throw new NotImplementedException();
}
