using System.Net.Mail;
using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Users;

public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string? value)
    {
        var trimmed = value?.Trim();

        // MailAddress also accepts "Name <address>" and similar forms; only a bare address is valid here.
        if (string.IsNullOrEmpty(trimmed) || !MailAddress.TryCreate(trimmed, out var address) || address.Address != trimmed)
            return Result<Email>.Failure(UserErrors.EmailInvalid);

        return Result<Email>.Success(new Email(trimmed.ToLowerInvariant()));
    }
}
