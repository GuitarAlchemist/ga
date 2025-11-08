namespace GA.Core.Collections;

[PublicAPI]
public abstract class LazyCollectionBase<T> : IReadOnlyCollection<T>
{
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
    ///     Gets the <see cref="IReadOnlyCollection{T}" />
    /// </summary>
    public IReadOnlyCollection<T> Value => _lazy.Value;


    public override string ToString()
    {
        return string.Join(_separator, Value.Select(item => item?.ToString()));
    }

    #region IReadOnlyCollection Members

    public IEnumerator<T> GetEnumerator()
    {
        return Value.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public int Count => Value.Count;

    #endregion
}
