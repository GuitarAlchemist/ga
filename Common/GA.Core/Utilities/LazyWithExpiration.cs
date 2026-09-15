namespace GA.Core.Utilities;

/// <summary>
///     A lazily computed value that is recomputed on the first access after it expires. The
///     expiration window starts on the first access to <see cref="Value" />, not at construction.
/// </summary>
/// <remarks>
///     Expiration is checked against a timestamp on access. It used to be driven by a thread-pool
///     task blocked in <c>Thread.Sleep</c> for each value, which starved unrelated pool work.
/// </remarks>
public class LazyWithExpiration<T>
{
    private readonly TimeSpan _expirationTime;
    private readonly Func<T> _func;
    private readonly TimeProvider _timeProvider;
    private Entry _entry;

    public LazyWithExpiration(
        Func<T> func,
        TimeSpan expirationTime)
        : this(func, expirationTime, TimeProvider.System)
    {
    }

    public LazyWithExpiration(
        Func<T> func,
        TimeSpan expirationTime,
        TimeProvider timeProvider)
    {
        _expirationTime = expirationTime;
        _func = func;
        _timeProvider = timeProvider;
        _entry = new(func);
    }

    public T Value
    {
        get
        {
            while (true)
            {
                var entry = Volatile.Read(ref _entry);
                var now = _timeProvider.GetTimestamp();
                if (entry.TryStart(now, out var startedAt)
                    || _timeProvider.GetElapsedTime(startedAt, now) < _expirationTime)
                {
                    return entry.Lazy.Value;
                }

                // Expired: replace this entry (unless another caller or Reset already did) and retry.
                Interlocked.CompareExchange(ref _entry, new(_func), entry);
            }
        }
    }

    public void Reset() => Volatile.Write(ref _entry, new(_func));

    private sealed class Entry(Func<T> func)
    {
        private const long NotStarted = long.MinValue;
        private long _startedAt = NotStarted;

        public Lazy<T> Lazy { get; } = new(func);

        /// <summary>Starts the expiration window at <paramref name="now" /> if this is the first access.</summary>
        /// <returns><c>true</c> when this call started the window.</returns>
        public bool TryStart(long now, out long startedAt)
        {
            var previous = Interlocked.CompareExchange(ref _startedAt, now, NotStarted);
            startedAt = previous == NotStarted ? now : previous;
            return previous == NotStarted;
        }
    }
}
