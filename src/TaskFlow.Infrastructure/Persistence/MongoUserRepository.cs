using MongoDB.Driver;
using TaskFlow.Application.Auth;
using TaskFlow.Domain.Common;
using TaskFlow.Domain.Users;

namespace TaskFlow.Infrastructure.Persistence;

public sealed class MongoUserRepository : IUserRepository
{
    private readonly IMongoCollection<User> _users;

    public MongoUserRepository(IMongoDatabase database) => _users = database.GetCollection<User>(MongoSetup.UsersCollection);

    public Task<Result> AddAsync(User user, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken) => throw new NotImplementedException();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();
}
