namespace TaskFlow.Domain.Users;

public sealed class User
{
    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    public static User Create(Email email, string passwordHash, DateTimeOffset now) => throw new NotImplementedException();
}
