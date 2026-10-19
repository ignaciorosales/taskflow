using TaskFlow.Domain.Common;

namespace TaskFlow.Application.Auth;

public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static IReadOnlyList<Error> Validate(string? password) => throw new NotImplementedException();
}
