using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Users;

public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string? value) => throw new NotImplementedException();
}
