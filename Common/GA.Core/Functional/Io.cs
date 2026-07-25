namespace GA.Core.Functional;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Represents an IO operation that can be deferred.
/// </summary>
/// <typeparam name="T">The type of the result</typeparam>
[PublicAPI]
public readonly record struct Io<T>
{
    private readonly Func<T> _operation;

    public Io(Func<T> operation) => _operation = operation;

    public T Run() => _operation();

    public Io<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        var operation = _operation;
        return new(() => mapper(operation()));
    }

    public Io<TResult> Bind<TResult>(Func<T, Io<TResult>> binder)
    {
        var operation = _operation;
        return new(() => binder(operation()).Run());
    }

    /// <summary>
    ///     Retries the operation up to <paramref name="maxAttempts"/> times, blocking the
    ///     calling thread for <paramref name="delay"/> between attempts. Synchronous by design
    ///     because <see cref="Io{T}"/> wraps a synchronous <see cref="Func{T}"/>; use
    ///     <see cref="RetryAsync"/> for a non-blocking equivalent.
    /// </summary>
    public Io<T> Retry(int maxAttempts, TimeSpan delay)
    {
        var operation = _operation;
        return new(() =>
        {
            var attempts = 0;
            while (true)
            {
                try
                {
                    attempts++;
                    return operation();
                }
                catch when (attempts < maxAttempts)
                {
                    Thread.Sleep(delay);
                }
            }
        });
    }

    /// <summary>
    ///     Async counterpart of <see cref="Retry"/> that awaits a non-blocking
    ///     <see cref="Task.Delay(TimeSpan)"/> between attempts instead of sleeping the thread.
    /// </summary>
    public async Task<T> RetryAsync(int maxAttempts, TimeSpan delay)
    {
        var operation = _operation;
        var attempts = 0;
        while (true)
        {
            try
            {
                attempts++;
                return operation();
            }
            catch when (attempts < maxAttempts)
            {
                await Task.Delay(delay);
            }
        }
    }
}

public static class Io
{
    public static Io<T> Of<T>(Func<T> operation) => new(operation);
}
