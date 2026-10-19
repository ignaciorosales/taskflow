using TaskFlow.Domain.Users;

namespace TaskFlow.Domain.Tests.Users;

public class EmailTests
{
    [Fact]
    public void Email_is_trimmed_and_lower_cased()
    {
        Assert.Equal("john.doe@example.com", Email.Create("  John.Doe@Example.COM \t").Value.Value);
    }

    [Fact]
    public void Emails_differing_only_in_case_and_spaces_are_equal()
    {
        Assert.Equal(Email.Create("john@example.com").Value, Email.Create(" JOHN@example.com ").Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("john")]
    [InlineData("john@")]
    [InlineData("@example.com")]
    [InlineData("john doe@example.com")]
    [InlineData("John <john@example.com>")]
    [InlineData("john@example.com, jane@example.com")]
    public void Malformed_email_is_rejected(string? value)
    {
        var result = Email.Create(value);

        Assert.True(result.IsFailure);
        Assert.Equal([UserErrors.EmailInvalid], result.Errors);
    }
}
