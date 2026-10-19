using MongoDB.Driver;
using TaskFlow.Application.Auth;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class MongoLoginAttemptRepository : ILoginAttemptRepository
{
    private readonly IMongoCollection<LoginAttempt> _attempts;

    public MongoLoginAttemptRepository(IMongoDatabase database)
        => _attempts = database.GetCollection<LoginAttempt>(MongoSetup.LoginAttemptsCollection);

    public Task<LoginAttempt?> GetAsync(string email, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task SaveAsync(LoginAttempt attempt, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task ResetAsync(string email, CancellationToken cancellationToken) => throw new NotImplementedException();
}
