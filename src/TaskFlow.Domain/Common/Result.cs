namespace TaskFlow.Domain.Common;

public class Result
{
    protected Result(IReadOnlyList<Error> errors) => Errors = errors;

    public bool IsSuccess => Errors.Count == 0;
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors { get; }

    public static Result Success() => new([]);
    public static Result Failure(params Error[] errors) => new(errors);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, IReadOnlyList<Error> errors) : base(errors) => _value = value;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value) => new(value, []);
    public static new Result<T> Failure(params Error[] errors) => new(default, errors);
}
