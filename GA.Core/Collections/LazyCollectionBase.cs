namespace GA.Core.Collections;

public abstract class LazyCollectionBase<TItem> : IReadOnlyCollection<TItem>
    where TItem : class
{
    private readonly Lazy<IReadOnlyCollection<TItem>> _lazy;

    protected LazyCollectionBase(IEnumerable<TItem> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        _lazy = new(items.ToImmutableList);
    }

    public IEnumerator<TItem> GetEnumerator() => _lazy.Value.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _lazy.Value.Count;
    public override string ToString() => string.Join(" ", _lazy.Value.Select(value => value.ToString()));
}