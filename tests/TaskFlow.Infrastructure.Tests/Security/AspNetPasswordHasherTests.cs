using TaskFlow.Infrastructure.Security;

namespace TaskFlow.Infrastructure.Tests.Security;

public class AspNetPasswordHasherTests
{
    private readonly AspNetPasswordHasher _hasher = new();

    [Fact]
    public void Hash_never_contains_the_password()
    {
        Assert.DoesNotContain("Secr3t!pass", _hasher.Hash("Secr3t!pass"));
    }

    [Fact]
    public void Same_password_hashes_differently_each_time()
    {
        Assert.NotEqual(_hasher.Hash("Secr3t!pass"), _hasher.Hash("Secr3t!pass"));
    }

    [Fact]
    public void Verify_accepts_the_right_password_only()
    {
        var hash = _hasher.Hash("Secr3t!pass");

        Assert.True(_hasher.Verify(hash, "Secr3t!pass"));
        Assert.False(_hasher.Verify(hash, "secr3t!pass"));
        Assert.False(_hasher.Verify(hash, "Secr3t!pass "));
    }
}
