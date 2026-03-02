namespace GA.Core.Functional;

using System;

/// <summary>
///     Represents an IO operation that can be deferred.
/// </summary>
/// <typeparam name="T">The type of the result</typeparam>
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
                    System.Threading.Thread.Sleep(delay);
                }
            }
        });
    }
}

public static class Io
{
    public static Io<T> Of<T>(Func<T> operation) => new(operation);
}
