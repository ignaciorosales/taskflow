namespace TaskFlow.Domain.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    TooManyRequests,
}

public sealed record Error(string Code, string Message, string? Field = null, ErrorType Type = ErrorType.Validation);
