namespace GA.Core.Functional;

using System.Threading.Tasks;

/// <summary>
///     Represents a computation that may either result in a value of type T or throw an exception.
/// </summary>
/// <typeparam name="T">The type of the result</typeparam>
[PublicAPI]
public readonly record struct Try<T>
{
    private readonly T? _value;
    private readonly Exception? _exception;

    private Try(T value)
    {
        _value = value;
        _exception = null;
    }

    private Try(Exception exception)
    {
        _value = default;
        _exception = exception;
    }

    public bool IsSuccess => _exception == null;
    public bool IsFailure => !IsSuccess;

    public static Try<T> Success(T value) => new(value);
    public static Try<T> Failure(Exception exception) => new(exception);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Exception, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(_exception!);

    public void Match(Action<T> onSuccess, Action<Exception> onFailure)
    {
        if (IsSuccess) onSuccess(_value!);
        else onFailure(_exception!);
    }

    public T GetValueOrThrow() => IsSuccess ? _value! : throw _exception!;
}

/// <summary>
///     Static factory methods for Try monad
/// </summary>
public static class Try
{
    public static Try<T> Of<T>(Func<T> operation)
    {
        try
        {
            return Try<T>.Success(operation());
        }
        catch (Exception ex)
        {
            return Try<T>.Failure(ex);
        }
    }

    public static async Task<Try<T>> OfAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            var result = await operation();
            return Try<T>.Success(result);
        }
        catch (Exception ex)
        {
            return Try<T>.Failure(ex);
        }
    }
}
