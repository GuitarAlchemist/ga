namespace GA.Core.Collections;

public sealed class PrintableReadOnlySet<T>(
    IImmutableSet<T> items,
    string? itemFormat = null,
    IFormatProvider? itemFormatProvider = null)
    : PrintableBase<T>(items, itemFormat, itemFormatProvider), IReadOnlySet<T>
{
    private readonly IImmutableSet<T> _items = items ?? throw new ArgumentNullException(nameof(items));

    public PrintableReadOnlySet(IImmutableSet<T> items)
        : this(items, null)
    {
    }

    public int Count => _items.Count;

    public bool Contains(T item)
    {
        return _items.Contains(item);
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        return _items.IsProperSubsetOf(other);
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        return _items.IsProperSupersetOf(other);
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        return _items.IsSubsetOf(other);
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        return _items.IsSupersetOf(other);
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        return _items.Overlaps(other);
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        return _items.SetEquals(other);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_items).GetEnumerator();
    }
}
