namespace GA.Core.Collections;

[PublicAPI]
public abstract class LazyCollectionBase<T> : IReadOnlyCollection<T>
    where T : class
{
    #region IReadOnlyCollection Members
    
    public IEnumerator<T> GetEnumerator() => Value.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => Value.Count;
    
    #endregion
    
    private readonly Lazy<IReadOnlyCollection<T>> _lazy;
    private readonly string _separator;

    protected LazyCollectionBase(
        IEnumerable<T> items,
        string separator = " ")
    {
        ArgumentNullException.ThrowIfNull(items);
        
        _lazy = new([..items]);
        _separator = separator;
    }

    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{T}"/>
    /// </summary>
    public IReadOnlyCollection<T> Value => _lazy.Value;


    public override string ToString() => string.Join(_separator, Value.Select(value => value.ToString()));
}