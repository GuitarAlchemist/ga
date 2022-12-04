namespace GA.Core.Collections;

public abstract class LazyCollectionBase<T> : IReadOnlyCollection<T>
    where T : class
{
    private readonly Lazy<IReadOnlyCollection<T>> _lazy;
    private readonly string _separator;

    protected LazyCollectionBase(
        IEnumerable<T> items,
        string separator)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        _lazy = new(items.ToImmutableList);
        _separator = separator;
    }

    protected LazyCollectionBase(IEnumerable<T> items) 
        : this(items, " ")
    {
    }

    public IEnumerator<T> GetEnumerator() => _lazy.Value.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _lazy.Value.Count;
    public override string ToString() => string.Join(_separator, _lazy.Value.Select(value => value.ToString()));
}