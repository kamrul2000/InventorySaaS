namespace InventorySaaS.Application.Common.Models;

public class Result
{
    protected Result(bool isSuccess, IEnumerable<string>? errors = null)
    {
        IsSuccess = isSuccess;
        Errors = errors?.ToList() ?? [];
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public List<string> Errors { get; }

    public static Result Success() => new(true);
    public static Result Failure(string error) => new(false, [error]);
    public static Result Failure(IEnumerable<string> errors) => new(false, errors);
}

public class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(true)
    {
        _value = value;
    }

    private Result(IEnumerable<string> errors) : base(false, errors)
    {
        _value = default;
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result.");

    public static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(string error) => new([error]);
    public new static Result<T> Failure(IEnumerable<string> errors) => new(errors);
}
