using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Users;

public static class UserErrors
{
    public static readonly Error EmailInvalid = new("user.email_invalid", "Email is not valid.", "email");
}
