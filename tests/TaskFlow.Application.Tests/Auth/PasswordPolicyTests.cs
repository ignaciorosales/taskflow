using TaskFlow.Application.Auth;

namespace TaskFlow.Application.Tests.Auth;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdef1!")]
    [InlineData("Contraseña1!")]
    [InlineData("long passphrase 1 with spaces!")]
    public void Strong_password_has_no_errors(string password)
    {
        Assert.Empty(PasswordPolicy.Validate(password));
    }

    [Fact]
    public void Seven_characters_is_too_short()
    {
        Assert.Equal([AuthErrors.PasswordTooShort], PasswordPolicy.Validate("Abcde1!"));
    }

    [Theory]
    [InlineData("12345678!", "letter")]
    [InlineData("Abcdefgh!", "digit")]
    [InlineData("Abcdefg1", "symbol")]
    [InlineData("Abcdef1 x", "symbol")]
    public void Each_missing_character_class_is_reported(string password, string missing)
    {
        var expected = missing switch
        {
            "letter" => AuthErrors.PasswordNeedsLetter,
            "digit" => AuthErrors.PasswordNeedsDigit,
            _ => AuthErrors.PasswordNeedsSymbol,
        };

        Assert.Equal([expected], PasswordPolicy.Validate(password));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Missing_password_reports_every_rule(string? password)
    {
        Assert.Equal(
            [AuthErrors.PasswordTooShort, AuthErrors.PasswordNeedsLetter, AuthErrors.PasswordNeedsDigit, AuthErrors.PasswordNeedsSymbol],
            PasswordPolicy.Validate(password));
    }
}
