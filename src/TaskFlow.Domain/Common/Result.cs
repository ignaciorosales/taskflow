namespace TaskFlow.Domain.Common;

public class Result
{
    public bool IsSuccess => throw new NotImplementedException();
    public bool IsFailure => throw new NotImplementedException();
    public IReadOnlyList<Error> Errors => throw new NotImplementedException();

    public static Result Success() => throw new NotImplementedException();
    public static Result Failure(params Error[] errors) => throw new NotImplementedException();
}

public sealed class Result<T> : Result
{
    public T Value => throw new NotImplementedException();

    public static Result<T> Success(T value) => throw new NotImplementedException();
    public static new Result<T> Failure(params Error[] errors) => throw new NotImplementedException();
}
