using TaskFlow.Domain.Common;

namespace TaskFlow.Application.Auth;

public static class PasswordPolicy
{
    public const int MinLength = 8;

    public static IReadOnlyList<Error> Validate(string? password)
    {
        var value = password ?? "";
        var errors = new List<Error>();
        if (value.Length < MinLength)
            errors.Add(AuthErrors.PasswordTooShort);
        if (!value.Any(char.IsLetter))
            errors.Add(AuthErrors.PasswordNeedsLetter);
        if (!value.Any(char.IsDigit))
            errors.Add(AuthErrors.PasswordNeedsDigit);
        if (!value.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
            errors.Add(AuthErrors.PasswordNeedsSymbol);
        return errors;
    }
}
