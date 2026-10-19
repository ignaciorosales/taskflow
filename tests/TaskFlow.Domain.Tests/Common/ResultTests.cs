using TaskFlow.Domain.Common;

namespace TaskFlow.Domain.Tests.Common;

public class ResultTests
{
    private static readonly Error SomeError = new("some.error", "Something is wrong.");

    [Fact]
    public void Failure_carries_its_errors()
    {
        var result = Result<int>.Failure(SomeError);

        Assert.True(result.IsFailure);
        Assert.Equal([SomeError], result.Errors);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    // A failure without errors would report IsSuccess, and Result<T> would hand out a default value.
    [Fact]
    public void Failure_without_errors_is_a_programming_error()
    {
        Assert.Throws<ArgumentException>(() => Result.Failure());
        Assert.Throws<ArgumentException>(() => Result<int>.Failure());
    }
}
