namespace TaskFlow.Domain.Common;

public sealed record Error(string Code, string Message, string? Field = null);
