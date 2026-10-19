using MongoDB.Driver;
using TaskFlow.Infrastructure.Persistence;
using Testcontainers.MongoDb;

namespace TaskFlow.Infrastructure.Tests;

/// <summary>One real Mongo container per test class; every test gets its own database inside it.</summary>
public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder("mongo:8.0").Build();
    private MongoClient _client = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _client = new MongoClient(_container.GetConnectionString());
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public async Task<IMongoDatabase> NewDatabaseAsync()
    {
        var database = _client.GetDatabase($"test_{Guid.NewGuid():N}");
        await MongoSetup.ConfigureAsync(database);
        return database;
    }
}
