namespace Ecommerce.OrderService.Application.Abstractions;

/// Why an operation did not succeed, in a form a caller can branch on. A string message
/// alone forces the edge to parse prose to decide on a status code.
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}

/// The outcome of a handler.
///
/// Expected failures are values: an order that does not exist is an ordinary answer to an
/// ordinary question, not an exception. Genuinely exceptional cases — a violated domain
/// invariant, a lost database connection — still throw.
public sealed class Result<TValue>
{
    private readonly TValue? _value;

    private Result(TValue value)
    {
        _value = value;
        IsSuccess = true;
        Error = Error.None;
    }

    private Result(Error error)
    {
        _value = default;
        IsSuccess = false;
        Error = error;
    }

    public bool IsSuccess { get; }

    public Error Error { get; }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"There is no value: {Error.Code} — {Error.Message}");

    public static Result<TValue> Success(TValue value) => new(value);

    public static Result<TValue> Failure(Error error) => new(error);
}
