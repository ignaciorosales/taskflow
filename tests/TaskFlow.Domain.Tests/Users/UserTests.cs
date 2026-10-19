using TaskFlow.Domain.Users;

namespace TaskFlow.Domain.Tests.Users;

public class UserTests
{
    [Fact]
    public void New_user_keeps_its_email_password_hash_and_creation_time()
    {
        var email = Email.Create("john@example.com").Value;
        var now = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

        var user = User.Create(email, "hashed-password", now);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal("hashed-password", user.PasswordHash);
        Assert.Equal(now, user.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void User_without_password_hash_is_a_programming_error(string passwordHash)
    {
        var email = Email.Create("john@example.com").Value;

        Assert.Throws<ArgumentException>(() => User.Create(email, passwordHash, DateTimeOffset.UnixEpoch));
    }
}
