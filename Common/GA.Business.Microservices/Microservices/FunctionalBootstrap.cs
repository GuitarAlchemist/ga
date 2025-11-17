namespace GA.Business.Core.Microservices.Microservices;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Functional microservices framework inspired by Spring Boot and F# monads
/// Implements: Option, Result, Reader, State, Writer, Async monads
/// </summary>

#region Option Monad (F# Option<'T>)

/// <summary>
///     Option monad - represents a value that may or may not exist
///     Replaces null references with explicit optionality
/// </summary>
public abstract record Option<T>
{
    public bool IsSome => this is Some;
    public bool IsNone => this is None;

    public static Option<T> OfNullable(T? value)
    {
        return value is null ? new None() : new Some(value);
    }

    public static Option<T> OfObj(object? obj)
    {
        return obj is T value ? new Some(value) : new None();
    }

    // Functor: Map
    public Option<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return this switch
        {
            Some s => new Option<TResult>.Some(mapper(s.Value)),
            None => new Option<TResult>.None(),
            _ => throw new InvalidOperationException()
        };
    }

    // Monad: Bind (SelectMany)
    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> binder)
    {
        return this switch
        {
            Some s => binder(s.Value),
            None => new Option<TResult>.None(),
            _ => throw new InvalidOperationException()
        };
    }

    // Applicative: Apply
    public Option<TResult> Apply<TResult>(Option<Func<T, TResult>> optionFunc)
    {
        return (optionFunc, this) switch
        {
            (Option<Func<T, TResult>>.Some f, Some v) => new Option<TResult>.Some(f.Value(v.Value)),
            _ => new Option<TResult>.None()
        };
    }

    // Get value or default
    public T GetOrElse(T defaultValue)
    {
        return this switch
        {
            Some s => s.Value,
            _ => defaultValue
        };
    }

    public T GetOrElse(Func<T> defaultFactory)
    {
        return this switch
        {
            Some s => s.Value,
            _ => defaultFactory()
        };
    }

    // Pattern matching
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone)
    {
        return this switch
        {
            Some s => onSome(s.Value),
            None => onNone(),
            _ => throw new InvalidOperationException()
        };
    }

    // Filter
    public Option<T> Filter(Func<T, bool> predicate)
    {
        return this switch
        {
            Some s when predicate(s.Value) => this,
            _ => new None()
        };
    }

    // LINQ support
    public Option<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return Map(selector);
    }

    public Option<TResult> SelectMany<TResult>(Func<T, Option<TResult>> selector)
    {
        return Bind(selector);
    }

    public Option<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, Option<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }

    public record Some(T Value) : Option<T>;

    public record None : Option<T>;
}

#endregion

#region Result Monad (F# Result<'T, 'TError>)

/// <summary>
///     Result monad - represents success or failure with error information
///     Railway-oriented programming for error handling
/// </summary>
public abstract record Result<TSuccess, TFailure>
{
    public bool IsSuccess => this is Success;
    public bool IsFailure => this is Failure;

    // Functor: Map
    public Result<TNewSuccess, TFailure> Map<TNewSuccess>(Func<TSuccess, TNewSuccess> mapper)
    {
        return this switch
        {
            Success s => new Result<TNewSuccess, TFailure>.Success(mapper(s.Value)),
            Failure f => new Result<TNewSuccess, TFailure>.Failure(f.Error),
            _ => throw new InvalidOperationException()
        };
    }

    // Map error
    public Result<TSuccess, TNewFailure> MapError<TNewFailure>(Func<TFailure, TNewFailure> mapper)
    {
        return this switch
        {
            Success s => new Result<TSuccess, TNewFailure>.Success(s.Value),
            Failure f => new Result<TSuccess, TNewFailure>.Failure(mapper(f.Error)),
            _ => throw new InvalidOperationException()
        };
    }

    // Monad: Bind
    public Result<TNewSuccess, TFailure> Bind<TNewSuccess>(
        Func<TSuccess, Result<TNewSuccess, TFailure>> binder)
    {
        return this switch
        {
            Success s => binder(s.Value),
            Failure f => new Result<TNewSuccess, TFailure>.Failure(f.Error),
            _ => throw new InvalidOperationException()
        };
    }

    // Applicative: Apply
    public Result<TNewSuccess, TFailure> Apply<TNewSuccess>(
        Result<Func<TSuccess, TNewSuccess>, TFailure> resultFunc)
    {
        return (resultFunc, this) switch
        {
            (Result<Func<TSuccess, TNewSuccess>, TFailure>.Success f, Success v) =>
                new Result<TNewSuccess, TFailure>.Success(f.Value(v.Value)),
            (Result<Func<TSuccess, TNewSuccess>, TFailure>.Failure f, _) =>
                new Result<TNewSuccess, TFailure>.Failure(f.Error),
            (_, Failure f) => new Result<TNewSuccess, TFailure>.Failure(f.Error),
            _ => throw new InvalidOperationException()
        };
    }

    // Pattern matching
    public TResult Match<TResult>(
        Func<TSuccess, TResult> onSuccess,
        Func<TFailure, TResult> onFailure)
    {
        return this switch
        {
            Success s => onSuccess(s.Value),
            Failure f => onFailure(f.Error),
            _ => throw new InvalidOperationException()
        };
    }

    // Tap (side effect)
    public Result<TSuccess, TFailure> Tap(Action<TSuccess> action)
    {
        if (this is Success s)
        {
            action(s.Value);
        }

        return this;
    }

    public Result<TSuccess, TFailure> TapError(Action<TFailure> action)
    {
        if (this is Failure f)
        {
            action(f.Error);
        }

        return this;
    }

    // Get value or throw
    public TSuccess GetValueOrThrow()
    {
        return this switch
        {
            Success s => s.Value,
            Failure f => throw new InvalidOperationException($"Result is failure: {f.Error}"),
            _ => throw new InvalidOperationException()
        };
    }

    public TFailure GetErrorOrThrow()
    {
        return this switch
        {
            Failure f => f.Error,
            Success => throw new InvalidOperationException("Result is success"),
            _ => throw new InvalidOperationException()
        };
    }

    // LINQ support
    public Result<TNewSuccess, TFailure> Select<TNewSuccess>(Func<TSuccess, TNewSuccess> selector)
    {
        return Map(selector);
    }

    public Result<TNewSuccess, TFailure> SelectMany<TNewSuccess>(
        Func<TSuccess, Result<TNewSuccess, TFailure>> selector)
    {
        return Bind(selector);
    }

    public Result<TNewSuccess, TFailure> SelectMany<TIntermediate, TNewSuccess>(
        Func<TSuccess, Result<TIntermediate, TFailure>> selector,
        Func<TSuccess, TIntermediate, TNewSuccess> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }

    public record Success(TSuccess Value) : Result<TSuccess, TFailure>;

    public record Failure(TFailure Error) : Result<TSuccess, TFailure>;
}

#endregion

#region Reader Monad (F# Reader<'Env, 'T>)

/// <summary>
///     Reader monad - dependency injection as a monad
///     Represents a computation that depends on an environment
/// </summary>
public record Reader<TEnv, T>(Func<TEnv, T> Run)
{
    // Ask - get the environment
    public static Reader<TEnv, TEnv> Ask => new(env => env);

    // Functor: Map
    public Reader<TEnv, TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new Reader<TEnv, TResult>(env => mapper(Run(env)));
    }

    // Monad: Bind
    public Reader<TEnv, TResult> Bind<TResult>(Func<T, Reader<TEnv, TResult>> binder)
    {
        return new Reader<TEnv, TResult>(env =>
        {
            var value = Run(env);
            return binder(value).Run(env);
        });
    }

    // Local - modify the environment
    public Reader<TNewEnv, T> Local<TNewEnv>(Func<TNewEnv, TEnv> modifier)
    {
        return new Reader<TNewEnv, T>(newEnv => Run(modifier(newEnv)));
    }

    // LINQ support
    public Reader<TEnv, TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return Map(selector);
    }

    public Reader<TEnv, TResult> SelectMany<TResult>(Func<T, Reader<TEnv, TResult>> selector)
    {
        return Bind(selector);
    }

    public Reader<TEnv, TResult> SelectMany<TIntermediate, TResult>(
        Func<T, Reader<TEnv, TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }
}

/// <summary>
///     Reader monad helpers
/// </summary>
public static class Reader
{
    public static Reader<TEnv, T> Return<TEnv, T>(T value)
    {
        return new Reader<TEnv, T>(_ => value);
    }

    public static Reader<TEnv, TEnv> Ask<TEnv>()
    {
        return Reader<TEnv, TEnv>.Ask;
    }
}

#endregion

#region State Monad (F# State<'S, 'T>)

/// <summary>
///     State monad - threading state through computations
///     Represents a stateful computation
/// </summary>
public record State<TState, T>(Func<TState, (T Value, TState State)> Run)
{
    // Get current state
    public static State<TState, TState> Get => new(state => (state, state));

    // Functor: Map
    public State<TState, TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new State<TState, TResult>(state =>
        {
            var (value, newState) = Run(state);
            return (mapper(value), newState);
        });
    }

    // Monad: Bind
    public State<TState, TResult> Bind<TResult>(Func<T, State<TState, TResult>> binder)
    {
        return new State<TState, TResult>(state =>
        {
            var (value, newState) = Run(state);
            return binder(value).Run(newState);
        });
    }

    // Set new state
    public static State<TState, Unit> Put(TState newState)
    {
        return new State<TState, Unit>(_ => (Unit.Value, newState));
    }

    // Modify state
    public static State<TState, Unit> Modify(Func<TState, TState> modifier)
    {
        return new State<TState, Unit>(state => (Unit.Value, modifier(state)));
    }

    // LINQ support
    public State<TState, TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return Map(selector);
    }

    public State<TState, TResult> SelectMany<TResult>(Func<T, State<TState, TResult>> selector)
    {
        return Bind(selector);
    }

    public State<TState, TResult> SelectMany<TIntermediate, TResult>(
        Func<T, State<TState, TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }
}

/// <summary>
///     State monad helpers
/// </summary>
public static class State
{
    public static State<TState, T> Return<TState, T>(T value)
    {
        return new State<TState, T>(state => (value, state));
    }
}

#endregion

#region Async Monad (F# Async<'T>)

/// <summary>
///     Async monad - asynchronous computations
///     Wraps Task<T> with monadic operations
/// </summary>
public record Async<T>(Func<Task<T>> Run)
{
    // Functor: Map
    public Async<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new Async<TResult>(async () => mapper(await Run()));
    }

    // Monad: Bind
    public Async<TResult> Bind<TResult>(Func<T, Async<TResult>> binder)
    {
        return new Async<TResult>(async () =>
        {
            var value = await Run();
            return await binder(value).Run();
        });
    }

    // Applicative: Apply
    public Async<TResult> Apply<TResult>(Async<Func<T, TResult>> asyncFunc)
    {
        return new Async<TResult>(async () =>
        {
            var func = await asyncFunc.Run();
            var value = await Run();
            return func(value);
        });
    }

    // LINQ support
    public Async<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return Map(selector);
    }

    public Async<TResult> SelectMany<TResult>(Func<T, Async<TResult>> selector)
    {
        return Bind(selector);
    }

    public Async<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, Async<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }

    // Convert to Task
    public Task<T> ToTask()
    {
        return Run();
    }
}

/// <summary>
///     Async monad helpers
/// </summary>
public static class Async
{
    public static Async<T> Return<T>(T value)
    {
        return new Async<T>(() => Task.FromResult(value));
    }

    public static Async<T> FromTask<T>(Task<T> task)
    {
        return new Async<T>(() => task);
    }
}

#endregion

#region Try Monad (Exception Handling - Spring @ExceptionHandler)

/// <summary>
///     Try monad - wraps exception-throwing code in a monad
///     Similar to Spring Boot's @ExceptionHandler pattern
/// </summary>
public abstract record Try<T>
{
    public bool IsSuccess => this is Success;
    public bool IsFailure => this is Failure;

    // Functor: Map
    public Try<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return this switch
        {
            Success s => Try.Of(() => mapper(s.Value)),
            Failure f => new Try<TResult>.Failure(f.Exception),
            _ => throw new InvalidOperationException()
        };
    }

    // Monad: Bind
    public Try<TResult> Bind<TResult>(Func<T, Try<TResult>> binder)
    {
        return this switch
        {
            Success s => Try.Of(() => binder(s.Value)) switch
            {
                Try<Try<TResult>>.Success inner => inner.Value,
                Try<Try<TResult>>.Failure f => new Try<TResult>.Failure(f.Exception),
                _ => throw new InvalidOperationException()
            },
            Failure f => new Try<TResult>.Failure(f.Exception),
            _ => throw new InvalidOperationException()
        };
    }

    // Pattern matching
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Exception, TResult> onFailure)
    {
        return this switch
        {
            Success s => onSuccess(s.Value),
            Failure f => onFailure(f.Exception),
            _ => throw new InvalidOperationException()
        };
    }

    // Convert to Result
    public Result<T, Exception> ToResult()
    {
        return this switch
        {
            Success s => new Result<T, Exception>.Success(s.Value),
            Failure f => new Result<T, Exception>.Failure(f.Exception),
            _ => throw new InvalidOperationException()
        };
    }

    // Recover from exception
    public Try<T> Recover(Func<Exception, T> recovery)
    {
        return this switch
        {
            Success => this,
            Failure f => Try.Of(() => recovery(f.Exception)),
            _ => throw new InvalidOperationException()
        };
    }

    // LINQ support
    public Try<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return Map(selector);
    }

    public Try<TResult> SelectMany<TResult>(Func<T, Try<TResult>> selector)
    {
        return Bind(selector);
    }

    public Try<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, Try<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }

    public record Success(T Value) : Try<T>;

    public record Failure(Exception Exception) : Try<T>;
}

/// <summary>
///     Try monad helpers
/// </summary>
public static class Try
{
    public static Try<T> Of<T>(Func<T> func)
    {
        try
        {
            return new Try<T>.Success(func());
        }
        catch (Exception ex)
        {
            return new Try<T>.Failure(ex);
        }
    }

    public static async Task<Try<T>> OfAsync<T>(Func<Task<T>> func)
    {
        try
        {
            var result = await func();
            return new Try<T>.Success(result);
        }
        catch (Exception ex)
        {
            return new Try<T>.Failure(ex);
        }
    }

    public static Try<T> Success<T>(T value)
    {
        return new Try<T>.Success(value);
    }

    public static Try<T> Failure<T>(Exception exception)
    {
        return new Try<T>.Failure(exception);
    }
}

#endregion

#region Either Monad (More General than Result)

/// <summary>
///     Either monad - represents one of two possible values
///     More general than Result - both sides are valid values
/// </summary>
public abstract record Either<TLeft, TRight>
{
    public bool IsLeft => this is Left;
    public bool IsRight => this is Right;

    // Functor: Map (maps the Right side)
    public Either<TLeft, TNewRight> Map<TNewRight>(Func<TRight, TNewRight> mapper)
    {
        return this switch
        {
            Right r => new Either<TLeft, TNewRight>.Right(mapper(r.Value)),
            Left l => new Either<TLeft, TNewRight>.Left(l.Value),
            _ => throw new InvalidOperationException()
        };
    }

    // Map the Left side
    public Either<TNewLeft, TRight> MapLeft<TNewLeft>(Func<TLeft, TNewLeft> mapper)
    {
        return this switch
        {
            Left l => new Either<TNewLeft, TRight>.Left(mapper(l.Value)),
            Right r => new Either<TNewLeft, TRight>.Right(r.Value),
            _ => throw new InvalidOperationException()
        };
    }

    // Monad: Bind
    public Either<TLeft, TNewRight> Bind<TNewRight>(Func<TRight, Either<TLeft, TNewRight>> binder)
    {
        return this switch
        {
            Right r => binder(r.Value),
            Left l => new Either<TLeft, TNewRight>.Left(l.Value),
            _ => throw new InvalidOperationException()
        };
    }

    // Pattern matching
    public TResult Match<TResult>(
        Func<TLeft, TResult> onLeft,
        Func<TRight, TResult> onRight)
    {
        return this switch
        {
            Left l => onLeft(l.Value),
            Right r => onRight(r.Value),
            _ => throw new InvalidOperationException()
        };
    }

    // Swap sides
    public Either<TRight, TLeft> Swap()
    {
        return this switch
        {
            Left l => new Either<TRight, TLeft>.Right(l.Value),
            Right r => new Either<TRight, TLeft>.Left(r.Value),
            _ => throw new InvalidOperationException()
        };
    }

    // LINQ support
    public Either<TLeft, TNewRight> Select<TNewRight>(Func<TRight, TNewRight> selector)
    {
        return Map(selector);
    }

    public Either<TLeft, TNewRight> SelectMany<TNewRight>(Func<TRight, Either<TLeft, TNewRight>> selector)
    {
        return Bind(selector);
    }

    public Either<TLeft, TNewRight> SelectMany<TIntermediate, TNewRight>(
        Func<TRight, Either<TLeft, TIntermediate>> selector,
        Func<TRight, TIntermediate, TNewRight> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }

    public record Left(TLeft Value) : Either<TLeft, TRight>;

    public record Right(TRight Value) : Either<TLeft, TRight>;
}

#endregion

#region Validation Monad (Accumulating Errors - Spring @Valid)

/// <summary>
///     Validation monad - accumulates errors instead of short-circuiting
///     Similar to Spring Boot's @Valid annotation with BindingResult
/// </summary>
public abstract record Validation<T, TError>
{
    public bool IsSuccess => this is Success;
    public bool IsFailure => this is Failure;

    // Functor: Map
    public Validation<TResult, TError> Map<TResult>(Func<T, TResult> mapper)
    {
        return this switch
        {
            Success s => new Validation<TResult, TError>.Success(mapper(s.Value)),
            Failure f => new Validation<TResult, TError>.Failure(f.Errors),
            _ => throw new InvalidOperationException()
        };
    }

    // Applicative: Apply (accumulates errors!)
    public Validation<TResult, TError> Apply<TResult>(
        Validation<Func<T, TResult>, TError> validationFunc)
    {
        return (validationFunc, this) switch
        {
            (Validation<Func<T, TResult>, TError>.Success f, Success v) => new Validation<TResult, TError>.Success(
                f.Value(v.Value)),
            (Validation<Func<T, TResult>, TError>.Failure f1, Failure f2) => new Validation<TResult, TError>.Failure(
                [.. f1.Errors, .. f2.Errors]),
            (Validation<Func<T, TResult>, TError>.Failure f, _) => new Validation<TResult, TError>.Failure(f.Errors),
            (_, Failure f) => new Validation<TResult, TError>.Failure(f.Errors),
            _ => throw new InvalidOperationException()
        };
    }

    // Pattern matching
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<IReadOnlyList<TError>, TResult> onFailure)
    {
        return this switch
        {
            Success s => onSuccess(s.Value),
            Failure f => onFailure(f.Errors),
            _ => throw new InvalidOperationException()
        };
    }

    // Combine validations (accumulates all errors)
    public static Validation<T, TError> Combine(params Validation<T, TError>[] validations)
    {
        var errors = new List<TError>();
        T? lastValue = default;

        foreach (var validation in validations)
        {
            if (validation is Failure f)
            {
                errors.AddRange(f.Errors);
            }
            else if (validation is Success s)
            {
                lastValue = s.Value;
            }
        }

        return errors.Count > 0
            ? new Failure(errors)
            : new Success(lastValue!);
    }

    public record Success(T Value) : Validation<T, TError>;

    public record Failure(IReadOnlyList<TError> Errors) : Validation<T, TError>;
}

/// <summary>
///     Validation monad helpers
/// </summary>
public static class Validation
{
    public static Validation<T, TError> Success<T, TError>(T value)
    {
        return new Validation<T, TError>.Success(value);
    }

    public static Validation<T, TError> Fail<T, TError>(TError error)
    {
        return new Validation<T, TError>.Failure([error]);
    }

    public static Validation<T, TError> Fail<T, TError>(params TError[] errors)
    {
        return new Validation<T, TError>.Failure(errors);
    }
}

#endregion

#region Writer Monad (Logging - Spring Logging Aspects)

/// <summary>
///     Writer monad - accumulates log messages alongside computation
///     Similar to Spring Boot's logging aspects and AOP
/// </summary>
public record Writer<TLog, T>(T Value, IReadOnlyList<TLog> Log)
{
    // Functor: Map
    public Writer<TLog, TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new Writer<TLog, TResult>(mapper(Value), Log);
    }

    // Monad: Bind
    public Writer<TLog, TResult> Bind<TResult>(Func<T, Writer<TLog, TResult>> binder)
    {
        var result = binder(Value);
        return new Writer<TLog, TResult>(result.Value, [.. Log, .. result.Log]);
    }

    // Tell - add log entry
    public Writer<TLog, T> Tell(TLog logEntry)
    {
        return new Writer<TLog, T>(Value, [.. Log, logEntry]);
    }

    public Writer<TLog, T> Tell(params TLog[] logEntries)
    {
        return new Writer<TLog, T>(Value, [.. Log, .. logEntries]);
    }

    // LINQ support
    public Writer<TLog, TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return Map(selector);
    }

    public Writer<TLog, TResult> SelectMany<TResult>(Func<T, Writer<TLog, TResult>> selector)
    {
        return Bind(selector);
    }

    public Writer<TLog, TResult> SelectMany<TIntermediate, TResult>(
        Func<T, Writer<TLog, TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }
}

/// <summary>
///     Writer monad helpers
/// </summary>
public static class Writer
{
    public static Writer<TLog, T> Return<TLog, T>(T value)
    {
        return new Writer<TLog, T>(value, []);
    }

    public static Writer<TLog, Unit> Tell<TLog>(TLog logEntry)
    {
        return new Writer<TLog, Unit>(Unit.Value, [logEntry]);
    }

    public static Writer<TLog, Unit> Tell<TLog>(params TLog[] logEntries)
    {
        return new Writer<TLog, Unit>(Unit.Value, logEntries);
    }
}

#endregion

#region IO Monad (Side Effects - Spring @Transactional)

/// <summary>
///     IO monad - represents side-effectful computations
///     Similar to Spring Boot's @Transactional and transaction management
/// </summary>
public record Io<T>(Func<T> UnsafeRun)
{
    // Functor: Map
    public Io<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new Io<TResult>(() => mapper(UnsafeRun()));
    }

    // Monad: Bind
    public Io<TResult> Bind<TResult>(Func<T, Io<TResult>> binder)
    {
        return new Io<TResult>(() => binder(UnsafeRun()).UnsafeRun());
    }

    // Delay execution
    public Io<T> Delay(TimeSpan delay)
    {
        return new Io<T>(() =>
        {
            Thread.Sleep(delay);
            return UnsafeRun();
        });
    }

    // Retry on failure
    public Io<T> Retry(int maxAttempts, TimeSpan delay)
    {
        return new Io<T>(() =>
        {
            Exception? lastException = null;
            for (var i = 0; i < maxAttempts; i++)
            {
                try
                {
                    return UnsafeRun();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (i < maxAttempts - 1)
                    {
                        Thread.Sleep(delay);
                    }
                }
            }

            throw lastException!;
        });
    }

    // LINQ support
    public Io<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return Map(selector);
    }

    public Io<TResult> SelectMany<TResult>(Func<T, Io<TResult>> selector)
    {
        return Bind(selector);
    }

    public Io<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, Io<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }
}

/// <summary>
///     IO monad helpers
/// </summary>
public static class Io
{
    public static Io<T> Return<T>(T value)
    {
        return new Io<T>(() => value);
    }

    public static Io<T> Of<T>(Func<T> func)
    {
        return new Io<T>(func);
    }

    public static Io<Unit> Run(Action action)
    {
        return new Io<Unit>(() =>
        {
            action();
            return Unit.Value;
        });
    }
}

#endregion

#region Lazy Monad (Lazy Initialization - Spring @Lazy)

/// <summary>
///     Lazy monad - deferred computation
///     Similar to Spring Boot's @Lazy annotation for lazy bean initialization
/// </summary>
public record LazyM<T>
{
    private readonly Lazy<T> _lazy;

    public LazyM(Func<T> factory)
    {
        _lazy = new Lazy<T>(factory);
    }

    public T Value => _lazy.Value;
    public bool IsValueCreated => _lazy.IsValueCreated;

    // Functor: Map
    public LazyM<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new LazyM<TResult>(() => mapper(Value));
    }

    // Monad: Bind
    public LazyM<TResult> Bind<TResult>(Func<T, LazyM<TResult>> binder)
    {
        return new LazyM<TResult>(() => binder(Value).Value);
    }

    // Force evaluation
    public T Force()
    {
        return Value;
    }

    // LINQ support
    public LazyM<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return Map(selector);
    }

    public LazyM<TResult> SelectMany<TResult>(Func<T, LazyM<TResult>> selector)
    {
        return Bind(selector);
    }

    public LazyM<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, LazyM<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }
}

/// <summary>
///     Lazy monad helpers
/// </summary>
public static class LazyM
{
    public static LazyM<T> Return<T>(T value)
    {
        return new LazyM<T>(() => value);
    }

    public static LazyM<T> Of<T>(Func<T> factory)
    {
        return new LazyM<T>(factory);
    }
}

#endregion

#region ServiceLocator Monad (Spring-like Service Location)

/// <summary>
///     ServiceLocator monad - type-safe service location
///     Similar to Spring's ApplicationContext.getBean()
/// </summary>
public record ServiceLocator<T>(Func<IServiceProvider, Option<T>> Locate)
{
    // Functor: Map
    public ServiceLocator<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new ServiceLocator<TResult>(provider => Locate(provider).Map(mapper));
    }

    // Monad: Bind
    public ServiceLocator<TResult> Bind<TResult>(Func<T, ServiceLocator<TResult>> binder)
    {
        return new ServiceLocator<TResult>(provider => Locate(provider).Match(
            service => binder(service).Locate(provider),
            () => new Option<TResult>.None()
        ));
    }

    // Run with service provider
    public Option<T> Run(IServiceProvider provider)
    {
        return Locate(provider);
    }

    // Get or throw
    public T GetOrThrow(IServiceProvider provider)
    {
        return Locate(provider).Match(
            service => service,
            () => throw new InvalidOperationException($"Service {typeof(T).Name} not found")
        );
    }

    // LINQ support
    public ServiceLocator<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return Map(selector);
    }

    public ServiceLocator<TResult> SelectMany<TResult>(Func<T, ServiceLocator<TResult>> selector)
    {
        return Bind(selector);
    }

    public ServiceLocator<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, ServiceLocator<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }
}

/// <summary>
///     ServiceLocator monad helpers
/// </summary>
public static class ServiceLocator
{
    public static ServiceLocator<T> Get<T>() where T : class
    {
        return new ServiceLocator<T>(provider => Option<T>.OfNullable(provider.GetService<T>()));
    }

    public static ServiceLocator<T> GetRequired<T>() where T : class
    {
        return new ServiceLocator<T>(provider =>
        {
            var service = provider.GetService<T>();
            return service != null
                ? new Option<T>.Some(service)
                : throw new InvalidOperationException($"Required service {typeof(T).Name} not found");
        });
    }
}

#endregion

#region Scope Monad (Scoped Dependencies - Spring @Scope)

/// <summary>
///     Scope monad - manages scoped dependencies
///     Similar to Spring Boot's @Scope annotation
/// </summary>
public record Scope<T>(Func<IServiceProvider, T> CreateScoped)
{
    // Functor: Map
    public Scope<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        return new Scope<TResult>(provider => mapper(CreateScoped(provider)));
    }

    // Monad: Bind
    public Scope<TResult> Bind<TResult>(Func<T, Scope<TResult>> binder)
    {
        return new Scope<TResult>(provider => binder(CreateScoped(provider)).CreateScoped(provider));
    }

    // Run in scope
    public T RunInScope(IServiceProvider provider)
    {
        return CreateScoped(provider);
    }

    // Run with new scope
    public T RunWithNewScope(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        return CreateScoped(scope.ServiceProvider);
    }

    // LINQ support
    public Scope<TResult> Select<TResult>(Func<T, TResult> selector)
    {
        return Map(selector);
    }

    public Scope<TResult> SelectMany<TResult>(Func<T, Scope<TResult>> selector)
    {
        return Bind(selector);
    }

    public Scope<TResult> SelectMany<TIntermediate, TResult>(
        Func<T, Scope<TIntermediate>> selector,
        Func<T, TIntermediate, TResult> resultSelector)
    {
        return Bind(outer => selector(outer).Map(inner => resultSelector(outer, inner)));
    }
}

/// <summary>
///     Scope monad helpers
/// </summary>
public static class Scope
{
    public static Scope<T> Return<T>(T value)
    {
        return new Scope<T>(_ => value);
    }

    public static Scope<T> Of<T>(Func<IServiceProvider, T> factory)
    {
        return new Scope<T>(factory);
    }
}

#endregion

#region Unit Type

/// <summary>
///     Unit type - represents void/no value (F# unit)
/// </summary>
public record Unit
{
    public static readonly Unit Value = new();

    private Unit()
    {
    }
}

#endregion

#region Spring Boot-Inspired Types (Using Monads)

/// <summary>
///     Service configuration with validation (Spring Boot @Configuration)
///     Uses Result monad for validation
/// </summary>
public record ServiceConfiguration<TConfig>(
    string Name,
    string ConfigSection,
    Func<TConfig, Result<Unit, IReadOnlyList<string>>> Validator,
    TConfig DefaultConfig
) where TConfig : class;

/// <summary>
///     Service factory with dependencies (Spring Boot @Bean)
///     Uses Reader monad for dependency injection
/// </summary>
public record ServiceFactory<TService>(
    string Name,
    Reader<IServiceProvider, TService> Factory,
    ServiceLifetime Lifetime,
    Option<Func<TService, Async<bool>>> HealthCheck
) where TService : class
{
    // Convenience constructor for simple factories
    public ServiceFactory(
        string name,
        Func<IServiceProvider, TService> factory,
        ServiceLifetime lifetime)
        : this(name, new Reader<IServiceProvider, TService>(factory), lifetime,
            new Option<Func<TService, Async<bool>>>.None())
    {
    }
}

/// <summary>
///     Conditional service registration (Spring Boot @Conditional)
/// </summary>
public abstract record ServiceCondition
{
    public record Always : ServiceCondition;

    public record WhenConfigured(string Section) : ServiceCondition;

    public record WhenPropertyEquals(string Key, string Value) : ServiceCondition;

    public record WhenBeanPresent(Type BeanType) : ServiceCondition;

    public record WhenBeanAbsent(Type BeanType) : ServiceCondition;

    public record Custom(Reader<IServiceProvider, bool> Predicate) : ServiceCondition;
}

/// <summary>
///     Microservice starter bundle (Spring Boot Starter)
///     Immutable, composable configuration
/// </summary>
public record MicroserviceStarter(
    string Name,
    string Version,
    IReadOnlyList<string> Dependencies,
    IReadOnlyList<object> Configurations,
    IReadOnlyList<object> Services,
    IReadOnlyList<ServiceCondition> Conditions,
    Option<Async<Unit>> OnStartup,
    Option<Async<Unit>> OnShutdown
);

#endregion

#region Auto-Configuration (Spring Boot @Configuration)

public static class AutoConfiguration
{
    public static ServiceConfiguration<TConfig> CreateConfig<TConfig>(
        string name,
        string section,
        Func<TConfig, Result<Unit, IReadOnlyList<string>>> validator,
        TConfig defaultConfig) where TConfig : class
    {
        return new ServiceConfiguration<TConfig>(name, section, validator, defaultConfig);
    }

    public static ServiceFactory<TService> CreateService<TService>(
        string name,
        Reader<IServiceProvider, TService> factory,
        ServiceLifetime lifetime) where TService : class
    {
        return new ServiceFactory<TService>(name, factory, lifetime, new Option<Func<TService, Async<bool>>>.None());
    }

    public static ServiceFactory<TService> CreateService<TService>(
        string name,
        Func<IServiceProvider, TService> factory,
        ServiceLifetime lifetime) where TService : class
    {
        return new ServiceFactory<TService>(name, new Reader<IServiceProvider, TService>(factory), lifetime,
            new Option<Func<TService, Async<bool>>>.None());
    }

    public static ServiceFactory<TService> WithHealthCheck<TService>(
        this ServiceFactory<TService> factory,
        Func<TService, Async<bool>> healthCheck) where TService : class
    {
        return factory with { HealthCheck = new Option<Func<TService, Async<bool>>>.Some(healthCheck) };
    }

    public static MicroserviceStarter CreateStarter(string name, string version)
    {
        return new MicroserviceStarter(
            name,
            version,
            [],
            [],
            [],
            [new ServiceCondition.Always()],
            new Option<Async<Unit>>.None(),
            new Option<Async<Unit>>.None());
    }

    public static MicroserviceStarter WithDependency(this MicroserviceStarter starter, string dependency)
    {
        return starter with { Dependencies = [.. starter.Dependencies, dependency] };
    }

    public static MicroserviceStarter WithConfiguration(this MicroserviceStarter starter, object config)
    {
        return starter with { Configurations = [.. starter.Configurations, config] };
    }

    public static MicroserviceStarter WithService(this MicroserviceStarter starter, object service)
    {
        return starter with { Services = [.. starter.Services, service] };
    }

    public static MicroserviceStarter WithCondition(this MicroserviceStarter starter, ServiceCondition condition)
    {
        return starter with { Conditions = [.. starter.Conditions, condition] };
    }

    public static MicroserviceStarter WithStartup(this MicroserviceStarter starter,
        Func<IServiceProvider, Task> onStartup)
    {
        return starter with
        {
            OnStartup = new Option<Async<Unit>>.Some(new Async<Unit>(async () =>
            {
                await onStartup(null!);
                return Unit.Value;
            }))
        };
    }

    public static MicroserviceStarter WithShutdown(this MicroserviceStarter starter,
        Func<IServiceProvider, Task> onShutdown)
    {
        return starter with
        {
            OnShutdown = new Option<Async<Unit>>.Some(new Async<Unit>(async () =>
            {
                await onShutdown(null!);
                return Unit.Value;
            }))
        };
    }
}

#endregion

#region Application Context (Spring Boot ApplicationContext with Monads)

public static class ApplicationContext
{
    // Evaluate condition using Reader monad
    public static Reader<IServiceProvider, bool> EvaluateCondition(ServiceCondition condition)
    {
        return condition switch
        {
            ServiceCondition.Always => Reader.Return<IServiceProvider, bool>(true),
            ServiceCondition.WhenConfigured c => new Reader<IServiceProvider, bool>(provider =>
                Option<IConfiguration>.OfNullable(provider.GetService<IConfiguration>())
                    .Map(config => config.GetSection(c.Section).Exists())
                    .GetOrElse(false)),
            ServiceCondition.WhenPropertyEquals p => new Reader<IServiceProvider, bool>(provider =>
                Option<IConfiguration>.OfNullable(provider.GetService<IConfiguration>())
                    .Map(config => config[p.Key] == p.Value)
                    .GetOrElse(false)),
            ServiceCondition.WhenBeanPresent b => new Reader<IServiceProvider, bool>(provider =>
                provider.GetService(b.BeanType) != null),
            ServiceCondition.WhenBeanAbsent b => new Reader<IServiceProvider, bool>(provider =>
                provider.GetService(b.BeanType) == null),
            ServiceCondition.Custom c => c.Predicate,
            _ => Reader.Return<IServiceProvider, bool>(false)
        };
    }

    // Check all conditions using Reader monad composition
    public static Reader<IServiceProvider, bool> CheckConditions(IReadOnlyList<ServiceCondition> conditions)
    {
        return new Reader<IServiceProvider, bool>(provider => conditions.All(c => EvaluateCondition(c).Run(provider)));
    }

    // Register service using Reader monad
    public static IServiceCollection RegisterService<TService>(
        this IServiceCollection services,
        ServiceFactory<TService> factory) where TService : class
    {
        var descriptor = factory.Lifetime switch
        {
            ServiceLifetime.Singleton => ServiceDescriptor.Singleton(sp => factory.Factory.Run(sp)),
            ServiceLifetime.Scoped => ServiceDescriptor.Scoped(sp => factory.Factory.Run(sp)),
            ServiceLifetime.Transient => ServiceDescriptor.Transient(sp => factory.Factory.Run(sp)),
            _ => throw new ArgumentException($"Unknown lifetime: {factory.Lifetime}")
        };

        services.Add(descriptor);
        return services;
    }

    // Get service as Option monad
    public static Option<T> GetServiceOption<T>(this IServiceProvider provider) where T : class
    {
        return Option<T>.OfNullable(provider.GetService<T>());
    }

    // Get required service as Result monad
    public static Result<T, string> GetServiceResult<T>(this IServiceProvider provider) where T : class
    {
        var service = provider.GetService<T>();
        return service != null
            ? new Result<T, string>.Success(service)
            : new Result<T, string>.Failure($"Service {typeof(T).Name} not found");
    }
}

#endregion
