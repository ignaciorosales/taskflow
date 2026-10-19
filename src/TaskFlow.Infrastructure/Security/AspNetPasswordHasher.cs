using TaskFlow.Application.Auth;

namespace TaskFlow.Infrastructure.Security;

public sealed class AspNetPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => throw new NotImplementedException();

    public bool Verify(string hash, string password) => throw new NotImplementedException();
}
