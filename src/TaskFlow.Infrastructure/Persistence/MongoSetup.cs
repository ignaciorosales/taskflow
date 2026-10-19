using MongoDB.Driver;

namespace TaskFlow.Infrastructure.Persistence;

public static class MongoSetup
{
    public const string UsersCollection = "users";
    public const string TasksCollection = "tasks";

    /// <summary>Registers the serialization mappings and creates the indexes. Safe to run more than once.</summary>
    public static Task ConfigureAsync(IMongoDatabase database, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
