namespace GA.Core.Utilities;

using System.Diagnostics;

/// <summary>
///     A lazily created value that is recreated on the first access after it expires. The expiration
///     clock starts when the value is created and is checked on access, so no thread waits for it.
/// </summary>
public class LazyWithExpiration<T>
{
    private readonly long _expirationTicks;
    private readonly Func<T> _func;
    private State _state = null!;

    public LazyWithExpiration(
        Func<T> func,
        TimeSpan expirationTime)
    {
        _expirationTicks = (long)(expirationTime.TotalSeconds * Stopwatch.Frequency);
        _func = func;

        Reset();
    }

    public T Value
    {
        get
        {
            var state = _state;
            if (state.Lazy.IsValueCreated && Stopwatch.GetTimestamp() - state.CreatedAt >= _expirationTicks)
            {
                // Only one caller replaces an expired state; the others read the replacement.
                Interlocked.CompareExchange(ref _state, NewState(), state);
                state = _state;
            }

            return state.Lazy.Value;
        }
    }

    public void Reset() => _state = NewState();

    private State NewState()
    {
        var state = new State();
        state.Lazy = new(() =>
        {
            var value = _func();
            state.CreatedAt = Stopwatch.GetTimestamp();
            return value;
        });
        return state;
    }

    private sealed class State
    {
        public Lazy<T> Lazy = null!;
        public long CreatedAt;
    }
}
