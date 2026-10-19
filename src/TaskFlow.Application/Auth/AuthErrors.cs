using TaskFlow.Domain.Common;

namespace TaskFlow.Application.Auth;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        new("auth.invalid_credentials", "Email or password is incorrect.", Type: ErrorType.Unauthorized);
    public static readonly Error LockedOut =
        new("auth.locked_out", "Too many failed attempts. Try again in a minute.", Type: ErrorType.TooManyRequests);
    public static readonly Error InvalidRefreshToken =
        new("auth.invalid_refresh_token", "Your session has expired. Please log in again.", Type: ErrorType.Unauthorized);
    public static readonly Error UserNotFound = new("auth.user_not_found", "User not found.", Type: ErrorType.NotFound);

    public static readonly Error PasswordTooShort =
        new("auth.password_too_short", $"Password must be at least {PasswordPolicy.MinLength} characters.", "password");
    public static readonly Error PasswordNeedsLetter = new("auth.password_needs_letter", "Password must contain a letter.", "password");
    public static readonly Error PasswordNeedsDigit = new("auth.password_needs_digit", "Password must contain a digit.", "password");
    public static readonly Error PasswordNeedsSymbol = new("auth.password_needs_symbol", "Password must contain a symbol.", "password");
}
