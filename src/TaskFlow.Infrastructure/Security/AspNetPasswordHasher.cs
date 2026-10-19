using Microsoft.AspNetCore.Identity;
using TaskFlow.Application.Auth;

namespace TaskFlow.Infrastructure.Security;

/// <summary>ASP.NET Core Identity's hasher (PBKDF2, salted), without the rest of Identity.</summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private static readonly PasswordHasher<object> Hasher = new();

    public string Hash(string password) => Hasher.HashPassword(null!, password);

    public bool Verify(string hash, string password)
        => Hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
}
