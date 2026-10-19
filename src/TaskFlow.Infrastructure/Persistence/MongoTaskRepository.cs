using MongoDB.Bson;
using MongoDB.Driver;
using TaskFlow.Application.Tasks;
using TaskFlow.Domain.Tasks;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class MongoTaskRepository : ITaskRepository
{
    private readonly IMongoCollection<TaskItem> _tasks;

    public MongoTaskRepository(IMongoDatabase database) => _tasks = database.GetCollection<TaskItem>(MongoSetup.TasksCollection);

    public async Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await _tasks.Find(t => t.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<TaskItem>> ListAsync(Guid ownerId, TaskQuery query, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var filter = Filter(ownerId, query, now);
        var total = await _tasks.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        // In long, since (page - 1) * pageSize can overflow an int; past the end there's nothing to fetch.
        var skip = (long)(query.Page - 1) * query.PageSize;
        if (skip >= total)
            return new PagedResult<TaskItem>([], query.Page, query.PageSize, total);

        var items = await _tasks.Aggregate()
            .Match(filter)
            // Mongo sorts nulls first ascending; this flag keeps tasks without a due date last in both directions.
            .AppendStage<BsonDocument>(new BsonDocument("$addFields",
                new BsonDocument("noDueDate", new BsonDocument("$eq", new BsonArray { "$dueDate", BsonNull.Value }))))
            .Sort(Sort(query))
            .Skip((int)skip) // below total, which is far below int.MaxValue
            .Limit(query.PageSize)
            .As<TaskItem>()
            .ToListAsync(cancellationToken);

        return new PagedResult<TaskItem>(items, query.Page, query.PageSize, total);
    }

    public Task AddAsync(TaskItem task, CancellationToken cancellationToken)
        => _tasks.InsertOneAsync(task, cancellationToken: cancellationToken);

    public Task UpdateAsync(TaskItem task, CancellationToken cancellationToken)
        => _tasks.ReplaceOneAsync(t => t.Id == task.Id, task, cancellationToken: cancellationToken);

    private static FilterDefinition<TaskItem> Filter(Guid ownerId, TaskQuery query, DateTimeOffset now)
    {
        var f = Builders<TaskItem>.Filter;
        var filter = f.Eq(t => t.OwnerId, ownerId) & f.Eq(t => t.DeletedAt, null);
        if (query.Status is { } status)
            filter &= f.Eq(t => t.Status, status);
        if (query.Priority is { } priority)
            filter &= f.Eq(t => t.Priority, priority);
        if (query.OverdueOnly)
            filter &= f.Lt(t => t.DueDate, now) & f.Ne(t => t.Status, TaskItemStatus.Done);
        return filter;
    }

    // Every sort ends in createdAt and _id so pages never repeat or skip tasks with equal keys.
    private static BsonDocument Sort(TaskQuery query)
    {
        var direction = query.Descending ? -1 : 1;
        return query.SortBy switch
        {
            TaskSortBy.DueDate => new BsonDocument { { "noDueDate", 1 }, { "dueDate", direction }, { "createdAt", -1 }, { "_id", -1 } },
            TaskSortBy.Priority => new BsonDocument { { "priority", direction }, { "createdAt", -1 }, { "_id", -1 } },
            _ => new BsonDocument { { "createdAt", direction }, { "_id", direction } },
        };
    }
}
