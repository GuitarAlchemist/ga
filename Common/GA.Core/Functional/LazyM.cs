namespace GA.Core.Functional;

/// <summary>
///     Represents a lazy computation.
/// </summary>
/// <typeparam name="T">The type of the result</typeparam>
[PublicAPI]
public readonly record struct LazyM<T>
{
    private readonly Lazy<T> _lazy;

    public LazyM(Lazy<T> lazy) => _lazy = lazy;

    public T Run() => _lazy.Value;

    public LazyM<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        var lazy = _lazy;
        return new(new Lazy<TResult>(() => mapper(lazy.Value)));
    }

    public LazyM<TResult> Bind<TResult>(Func<T, LazyM<TResult>> binder)
    {
        var lazy = _lazy;
        return new(new Lazy<TResult>(() => binder(lazy.Value).Run()));
    }
}

public static class LazyM
{
    public static LazyM<T> Of<T>(Func<T> operation) => new(new Lazy<T>(operation));
}
