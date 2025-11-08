namespace GA.Core.Functional;

/// <summary>
///     Represents the result of an operation that can succeed with a value or fail with an error.
///     Implements the Result monad for functional error handling.
/// </summary>
/// <typeparam name="TValue">The type of the success value</typeparam>
/// <typeparam name="TError">The type of the error</typeparam>
/// <remarks>
///     This type enables railway-oriented programming where operations can be chained
///     and errors propagate automatically without throwing exceptions.
///     Example usage:
///     <code>
/// var result = Str.TryCreate(userInput)
///     .Bind(str => ValidateString(str))
///     .Map(str => str.Value);
/// 
/// result.Match(
///     onSuccess: value => Console.WriteLine($"Success: {value}"),
///     onFailure: error => Console.WriteLine($"Error: {error}")
/// );
/// </code>
/// </remarks>
[PublicAPI]
public readonly record struct Result<TValue, TError>
{
    private readonly TError? _error;
    private readonly TValue? _value;

    private Result(TValue value)
    {
        _value = value;
        _error = default;
        IsSuccess = true;
    }

    private Result(TError error)
    {
        _value = default;
        _error = error;
        IsSuccess = false;
    }

    /// <summary>
    ///     Gets whether this result represents a successful operation.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    ///     Gets whether this result represents a failed operation.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    ///     Creates a successful result with the given value.
    /// </summary>
    public static Result<TValue, TError> Success(TValue value)
    {
        return new Result<TValue, TError>(value);
    }

    /// <summary>
    ///     Creates a failed result with the given error.
    /// </summary>
    public static Result<TValue, TError> Failure(TError error)
    {
        return new Result<TValue, TError>(error);
    }

    /// <summary>
    ///     Functor: Maps the success value to a new value using the provided function.
    ///     If this result is a failure, the error is propagated unchanged.
    /// </summary>
    /// <remarks>
    ///     Satisfies functor laws:
    ///     - Identity: result.Map(x => x) == result
    ///     - Composition: result.Map(f).Map(g) == result.Map(x => g(f(x)))
    /// </remarks>
    public Result<TResult, TError> Map<TResult>(Func<TValue, TResult> mapper)
    {
        return IsSuccess
            ? Result<TResult, TError>.Success(mapper(_value!))
            : Result<TResult, TError>.Failure(_error!);
    }

    /// <summary>
    ///     Monad: Binds (FlatMaps) the success value to a new result using the provided function.
    ///     If this result is a failure, the error is propagated unchanged.
    /// </summary>
    /// <remarks>
    ///     Satisfies monad laws:
    ///     - Left Identity: Result.Success(a).Bind(f) == f(a)
    ///     - Right Identity: m.Bind(Result.Success) == m
    ///     - Associativity: m.Bind(f).Bind(g) == m.Bind(x => f(x).Bind(g))
    /// </remarks>
    public Result<TResult, TError> Bind<TResult>(Func<TValue, Result<TResult, TError>> binder)
    {
        return IsSuccess
            ? binder(_value!)
            : Result<TResult, TError>.Failure(_error!);
    }

    /// <summary>
    ///     Pattern matching: Executes one of two functions depending on whether this is a success or failure.
    /// </summary>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <summary>
    ///     Pattern matching (void): Executes one of two actions depending on whether this is a success or failure.
    /// </summary>
    public void Match(Action<TValue> onSuccess, Action<TError> onFailure)
    {
        if (IsSuccess)
        {
            onSuccess(_value!);
        }
        else
        {
            onFailure(_error!);
        }
    }

    /// <summary>
    ///     Gets the success value or returns the provided default value if this is a failure.
    /// </summary>
    public TValue GetValueOrDefault(TValue defaultValue = default!)
    {
        return IsSuccess ? _value! : defaultValue;
    }

    /// <summary>
    ///     Gets the success value or throws an exception if this is a failure.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is in a failure state.</exception>
    public TValue GetValueOrThrow()
    {
        return IsSuccess
            ? _value!
            : throw new InvalidOperationException($"Result is in failure state: {_error}");
    }

    /// <summary>
    ///     Gets the error value or throws an exception if this is a success.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is in a success state.</exception>
    public TError GetErrorOrThrow()
    {
        return IsFailure
            ? _error!
            : throw new InvalidOperationException($"Result is in success state: {_value}");
    }

    /// <summary>
    ///     Executes the provided action if this is a success, and returns this result unchanged.
    ///     Useful for side effects in a chain.
    /// </summary>
    public Result<TValue, TError> Tap(Action<TValue> action)
    {
        if (IsSuccess)
        {
            action(_value!);
        }

        return this;
    }

    /// <summary>
    ///     Executes the provided action if this is a failure, and returns this result unchanged.
    ///     Useful for logging errors in a chain.
    /// </summary>
    public Result<TValue, TError> TapError(Action<TError> action)
    {
        if (IsFailure)
        {
            action(_error!);
        }

        return this;
    }

    /// <summary>
    ///     Maps the error to a new error type using the provided function.
    ///     If this result is a success, the value is propagated unchanged.
    /// </summary>
    public Result<TValue, TNewError> MapError<TNewError>(Func<TError, TNewError> mapper)
    {
        return IsSuccess
            ? Result<TValue, TNewError>.Success(_value!)
            : Result<TValue, TNewError>.Failure(mapper(_error!));
    }

    /// <summary>
    ///     Converts this result to an Option, discarding the error if present.
    /// </summary>
    public Option<TValue> ToOption()
    {
        return IsSuccess ? Option<TValue>.Some(_value!) : Option<TValue>.None;
    }

    /// <summary>
    ///     Implicit conversion from TValue to Result (success).
    /// </summary>
    public static implicit operator Result<TValue, TError>(TValue value)
    {
        return Success(value);
    }

    public override string ToString()
    {
        return IsSuccess ? $"Success({_value})" : $"Failure({_error})";
    }
}

/// <summary>
///     Extension methods for Result monad.
/// </summary>
[PublicAPI]
public static class ResultExtensions
{
    /// <summary>
    ///     Flattens a nested Result into a single Result.
    /// </summary>
    public static Result<TValue, TError> Flatten<TValue, TError>(
        this Result<Result<TValue, TError>, TError> result)
    {
        return result.Bind(inner => inner);
    }

    /// <summary>
    ///     Combines two results using the provided combiner function.
    ///     If either result is a failure, returns the first failure encountered.
    /// </summary>
    public static Result<TResult, TError> Combine<T1, T2, TResult, TError>(
        this Result<T1, TError> result1,
        Result<T2, TError> result2,
        Func<T1, T2, TResult> combiner)
    {
        if (result1.IsFailure)
        {
            return Result<TResult, TError>.Failure(result1.GetErrorOrThrow());
        }

        if (result2.IsFailure)
        {
            return Result<TResult, TError>.Failure(result2.GetErrorOrThrow());
        }

        return Result<TResult, TError>.Success(combiner(
            result1.GetValueOrThrow(),
            result2.GetValueOrThrow()));
    }

    /// <summary>
    ///     Sequences a collection of results into a result of a collection.
    ///     If any result is a failure, returns the first failure encountered.
    /// </summary>
    public static Result<ImmutableList<TValue>, TError> Sequence<TValue, TError>(
        this IEnumerable<Result<TValue, TError>> results)
    {
        var values = ImmutableList.CreateBuilder<TValue>();

        foreach (var result in results)
        {
            if (result.IsFailure)
            {
                return Result<ImmutableList<TValue>, TError>.Failure(result.GetErrorOrThrow());
            }

            values.Add(result.GetValueOrThrow());
        }

        return Result<ImmutableList<TValue>, TError>.Success(values.ToImmutable());
    }

    /// <summary>
    ///     Traverses a collection, applying a function that returns a Result to each element,
    ///     and sequences the results.
    /// </summary>
    public static Result<ImmutableList<TResult>, TError> Traverse<TValue, TResult, TError>(
        this IEnumerable<TValue> values,
        Func<TValue, Result<TResult, TError>> func)
    {
        return values.Select(func).Sequence();
    }
}
