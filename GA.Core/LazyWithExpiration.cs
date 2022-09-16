namespace GA.Core;

public class LazyWithExpiration<T>
{
    private volatile bool expired;
    private TimeSpan expirationTime;
    private Func<T> func;
    private Lazy<T> lazyObject;

    public LazyWithExpiration( Func<T> func, TimeSpan expirationTime )
    {
        this.expirationTime = expirationTime;
        this.func = func;

        Reset();
    }

    public void Reset()
    {
        lazyObject = new( func );
        expired = false;
    }

    public T Value
    {
        get
        {
            if ( expired )
                Reset();

            if ( !lazyObject.IsValueCreated )
            {
                Task.Factory.StartNew( () =>
                {
                    Thread.Sleep( expirationTime );
                    expired = true;
                } );
            }

            return lazyObject.Value;
        }
    }

}