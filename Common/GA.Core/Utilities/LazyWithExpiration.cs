namespace GA.Core.Utilities;

using System.Threading;

/// <summary>
///     A lazily-computed value that automatically recomputes itself once it has expired.
/// </summary>
/// <typeparam name="T">The type of the value</typeparam>
public class LazyWithExpiration<T>
{
    private readonly TimeSpan _expirationTime;
    private readonly Func<T> _func;
    private readonly Func<DateTimeOffset> _timeProvider;
    private readonly Lock _lock = new();
    private T _value = default!;
    private DateTimeOffset _expiresAt;
    private bool _hasValue;

    /// <summary>
    ///     Initializes a new instance of the LazyWithExpiration class
    /// </summary>
    /// <param name="func">The factory function used to (re)compute the value</param>
    /// <param name="expirationTime">The duration for which a computed value remains valid</param>
    public LazyWithExpiration(
        Func<T> func,
        TimeSpan expirationTime)
        : this(func, expirationTime, () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the LazyWithExpiration class with an injectable time source.
    ///     Intended for deterministic testing; defaults to the real clock otherwise.
    /// </summary>
    /// <param name="func">The factory function used to (re)compute the value</param>
    /// <param name="expirationTime">The duration for which a computed value remains valid</param>
    /// <param name="timeProvider">A function returning the current time</param>
    public LazyWithExpiration(
        Func<T> func,
        TimeSpan expirationTime,
        Func<DateTimeOffset> timeProvider)
    {
        _func = func;
        _expirationTime = expirationTime;
        _timeProvider = timeProvider;

        Reset();
    }

    /// <summary>
    ///     Gets the current value, recomputing it first if it is missing or has expired
    /// </summary>
    public T Value
    {
        get
        {
            lock (_lock)
            {
                if (!_hasValue || _timeProvider() >= _expiresAt)
                {
                    _value = _func();
                    _expiresAt = _timeProvider() + _expirationTime;
                    _hasValue = true;
                }

                return _value;
            }
        }
    }

    /// <summary>
    ///     Forces the value to be recomputed on the next access
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _hasValue = false;
        }
    }
}
