using MongoDB.Driver;
using TaskFlow.Application.Auth;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class MongoLoginAttemptRepository : ILoginAttemptRepository
{
    private readonly IMongoCollection<LoginAttempt> _attempts;

    public MongoLoginAttemptRepository(IMongoDatabase database)
        => _attempts = database.GetCollection<LoginAttempt>(MongoSetup.LoginAttemptsCollection);

    public async Task<LoginAttempt?> GetAsync(string email, CancellationToken cancellationToken)
        => await _attempts.Find(a => a.Email == email).FirstOrDefaultAsync(cancellationToken);

    public Task SaveAsync(LoginAttempt attempt, CancellationToken cancellationToken)
        => _attempts.ReplaceOneAsync(a => a.Email == attempt.Email, attempt, new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public Task ResetAsync(string email, CancellationToken cancellationToken)
        => _attempts.DeleteOneAsync(a => a.Email == email, cancellationToken);
}
