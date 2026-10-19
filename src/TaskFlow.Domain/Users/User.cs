namespace TaskFlow.Domain.Users;

public sealed class User
{
    private User() { }

    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    public static User Create(Email email, string passwordHash, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        return new User { Id = Guid.NewGuid(), Email = email, PasswordHash = passwordHash, CreatedAt = now };
    }
}
