namespace GA.Core;

public class LazyWithExpiration<T>
{
    private readonly TimeSpan _expirationTime;
    private readonly Func<T> _func;
    private volatile bool _expired;
    private Lazy<T> _lazyObject = null!;

    public LazyWithExpiration(
        Func<T> func,
        TimeSpan expirationTime)
    {
        _expirationTime = expirationTime;
        _func = func;

        Reset();
    }

    public T Value
    {
        get
        {
            if (_expired)
            {
                Reset();
            }

            if (!_lazyObject.IsValueCreated)
            {
                Task.Factory.StartNew(() =>
                {
                    Thread.Sleep(_expirationTime);
                    _expired = true;
                });
            }

            return _lazyObject.Value;
        }
    }

    public void Reset()
    {
        _lazyObject = new(_func);
        _expired = false;
    }
}
