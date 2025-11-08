namespace GA.Business.Core.Microservices;

using System.Diagnostics;
using System.Diagnostics.Metrics;

/// <summary>
///     Performance metrics for monadic operations
///     Tracks success/failure rates, execution times, and cache hit rates
/// </summary>
public class MonadMetrics
{
    private static readonly Meter Meter = new("GA.Monads", "1.0.0");

    // Counters for monad operations
    private static readonly Counter<long> TrySuccessCounter = Meter.CreateCounter<long>(
        "monad.try.success",
        description: "Number of successful Try monad operations");

    private static readonly Counter<long> TryFailureCounter = Meter.CreateCounter<long>(
        "monad.try.failure",
        description: "Number of failed Try monad operations");

    private static readonly Counter<long> OptionSomeCounter = Meter.CreateCounter<long>(
        "monad.option.some",
        description: "Number of Option.Some values");

    private static readonly Counter<long> OptionNoneCounter = Meter.CreateCounter<long>(
        "monad.option.none",
        description: "Number of Option.None values");

    private static readonly Counter<long> ResultSuccessCounter = Meter.CreateCounter<long>(
        "monad.result.success",
        description: "Number of successful Result monad operations");

    private static readonly Counter<long> ResultFailureCounter = Meter.CreateCounter<long>(
        "monad.result.failure",
        description: "Number of failed Result monad operations");

    private static readonly Counter<long> ValidationSuccessCounter = Meter.CreateCounter<long>(
        "monad.validation.success",
        description: "Number of successful Validation monad operations");

    private static readonly Counter<long> ValidationFailureCounter = Meter.CreateCounter<long>(
        "monad.validation.failure",
        description: "Number of failed Validation monad operations");

    // Histograms for execution times
    private static readonly Histogram<double> TryExecutionTime = Meter.CreateHistogram<double>(
        "monad.try.execution_time",
        "ms",
        "Execution time of Try monad operations");

    private static readonly Histogram<double> ResultExecutionTime = Meter.CreateHistogram<double>(
        "monad.result.execution_time",
        "ms",
        "Execution time of Result monad operations");

    // Cache metrics
    private static readonly Counter<long> CacheHitCounter = Meter.CreateCounter<long>(
        "monad.cache.hit",
        description: "Number of cache hits");

    private static readonly Counter<long> CacheMissCounter = Meter.CreateCounter<long>(
        "monad.cache.miss",
        description: "Number of cache misses");

    /// <summary>
    ///     Record a Try monad success
    /// </summary>
    public static void RecordTrySuccess(string operation, double executionTimeMs)
    {
        TrySuccessCounter.Add(1, new KeyValuePair<string, object?>("operation", operation));
        TryExecutionTime.Record(executionTimeMs, new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    ///     Record a Try monad failure
    /// </summary>
    public static void RecordTryFailure(string operation, string errorType, double executionTimeMs)
    {
        TryFailureCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("error_type", errorType));
        TryExecutionTime.Record(executionTimeMs, new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    ///     Record an Option.Some value
    /// </summary>
    public static void RecordOptionSome(string operation)
    {
        OptionSomeCounter.Add(1, new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    ///     Record an Option.None value
    /// </summary>
    public static void RecordOptionNone(string operation)
    {
        OptionNoneCounter.Add(1, new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    ///     Record a Result monad success
    /// </summary>
    public static void RecordResultSuccess(string operation, double executionTimeMs)
    {
        ResultSuccessCounter.Add(1, new KeyValuePair<string, object?>("operation", operation));
        ResultExecutionTime.Record(executionTimeMs, new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    ///     Record a Result monad failure
    /// </summary>
    public static void RecordResultFailure(string operation, string errorType, double executionTimeMs)
    {
        ResultFailureCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("error_type", errorType));
        ResultExecutionTime.Record(executionTimeMs, new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    ///     Record a Validation monad success
    /// </summary>
    public static void RecordValidationSuccess(string operation)
    {
        ValidationSuccessCounter.Add(1, new KeyValuePair<string, object?>("operation", operation));
    }

    /// <summary>
    ///     Record a Validation monad failure
    /// </summary>
    public static void RecordValidationFailure(string operation, int errorCount)
    {
        ValidationFailureCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("error_count", errorCount));
    }

    /// <summary>
    ///     Record a cache hit
    /// </summary>
    public static void RecordCacheHit(string cacheKey)
    {
        CacheHitCounter.Add(1, new KeyValuePair<string, object?>("cache_key", cacheKey));
    }

    /// <summary>
    ///     Record a cache miss
    /// </summary>
    public static void RecordCacheMiss(string cacheKey)
    {
        CacheMissCounter.Add(1, new KeyValuePair<string, object?>("cache_key", cacheKey));
    }

    /// <summary>
    ///     Execute an operation and measure its execution time
    /// </summary>
    public static async Task<Try<T>> MeasureTryAsync<T>(string operation, Func<Task<T>> func)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await func();
            stopwatch.Stop();
            RecordTrySuccess(operation, stopwatch.Elapsed.TotalMilliseconds);
            return new Try<T>.Success(result);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            RecordTryFailure(operation, ex.GetType().Name, stopwatch.Elapsed.TotalMilliseconds);
            return new Try<T>.Failure(ex);
        }
    }

    /// <summary>
    ///     Execute an operation and measure its execution time, returning a Result
    /// </summary>
    public static async Task<Result<TSuccess, TFailure>> MeasureResultAsync<TSuccess, TFailure>(
        string operation,
        Func<Task<Result<TSuccess, TFailure>>> func,
        Func<TFailure, string> getErrorType)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await func();
        stopwatch.Stop();

        return result.Match(
            success =>
            {
                RecordResultSuccess(operation, stopwatch.Elapsed.TotalMilliseconds);
                return result;
            },
            failure =>
            {
                RecordResultFailure(operation, getErrorType(failure), stopwatch.Elapsed.TotalMilliseconds);
                return result;
            }
        );
    }

    /// <summary>
    ///     Track an Option value
    /// </summary>
    public static Option<T> TrackOption<T>(string operation, Option<T> option)
    {
        return option.Match(
            value =>
            {
                RecordOptionSome(operation);
                return option;
            },
            () =>
            {
                RecordOptionNone(operation);
                return option;
            }
        );
    }

    /// <summary>
    ///     Track a Validation value
    /// </summary>
    public static Validation<TSuccess, TFailure> TrackValidation<TSuccess, TFailure>(
        string operation,
        Validation<TSuccess, TFailure> validation)
    {
        return validation.Match(
            _ =>
            {
                RecordValidationSuccess(operation);
                return validation;
            },
            errors =>
            {
                RecordValidationFailure(operation, errors.Count());
                return validation;
            }
        );
    }
}

/// <summary>
///     Extension methods for tracking monad metrics
/// </summary>
public static class MonadMetricsExtensions
{
    /// <summary>
    ///     Track metrics for a Try monad
    /// </summary>
    public static Try<T> WithMetrics<T>(this Try<T> tryValue, string operation)
    {
        return tryValue.Match(
            _ =>
            {
                MonadMetrics.RecordTrySuccess(operation, 0);
                return tryValue;
            },
            ex =>
            {
                MonadMetrics.RecordTryFailure(operation, ex.GetType().Name, 0);
                return tryValue;
            }
        );
    }

    /// <summary>
    ///     Track metrics for an Option monad
    /// </summary>
    public static Option<T> WithMetrics<T>(this Option<T> option, string operation)
    {
        return MonadMetrics.TrackOption(operation, option);
    }

    /// <summary>
    ///     Track metrics for a Result monad
    /// </summary>
    public static Result<TSuccess, TFailure> WithMetrics<TSuccess, TFailure>(
        this Result<TSuccess, TFailure> result,
        string operation,
        Func<TFailure, string> getErrorType)
    {
        return result.Match(
            _ =>
            {
                MonadMetrics.RecordResultSuccess(operation, 0);
                return result;
            },
            failure =>
            {
                MonadMetrics.RecordResultFailure(operation, getErrorType(failure), 0);
                return result;
            }
        );
    }

    /// <summary>
    ///     Track metrics for a Validation monad
    /// </summary>
    public static Validation<TSuccess, TFailure> WithMetrics<TSuccess, TFailure>(
        this Validation<TSuccess, TFailure> validation,
        string operation)
    {
        return MonadMetrics.TrackValidation(operation, validation);
    }
}
