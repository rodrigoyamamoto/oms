namespace Oms.Orders.Domain.Common;

internal sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    private Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        if (isSuccess && value is null)
        {
            throw new ArgumentNullException(nameof(value), "A successful result must contain a non-null value.");
        }

        _value = value;
    }

    public TValue Value =>
        IsSuccess
            ? _value!
            : throw new InvalidOperationException($"Cannot access the value of a failed result ('{Error.Code}').");

    public static Result<TValue> Success(TValue value) => new(value, true, Error.None);

    public new static Result<TValue> Failure(Error error) => new(default, false, error);
}
