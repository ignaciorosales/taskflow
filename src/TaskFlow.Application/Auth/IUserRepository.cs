using TaskFlow.Domain.Common;
using TaskFlow.Domain.Users;

namespace TaskFlow.Application.Auth;

public interface IUserRepository
{
    /// <summary>Fails with <see cref="UserErrors.EmailTaken"/> when the email is already registered.</summary>
    Task<Result> AddAsync(User user, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
