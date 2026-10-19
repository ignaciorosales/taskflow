using MongoDB.Driver;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Users;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public MongoUserRepository(IMongoDatabase database) => _users = database.GetCollection<User>(MongoSetup.UsersCollection);

    public async Task<Result> AddAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await _users.InsertOneAsync(user, cancellationToken: cancellationToken);
            return Result.Success();
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // The unique index on email is the source of truth, so concurrent registrations can't both win.
            return Result.Failure(UserErrors.EmailTaken);
        }
    }

    public async Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken)
        => await _users.Find(u => u.Email == email).FirstOrDefaultAsync(cancellationToken);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => await _users.Find(u => u.Id == id).FirstOrDefaultAsync(cancellationToken);
}
