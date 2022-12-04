namespace GA.Core.Collections;

public sealed class PrintableReadOnlySet<T> : PrintableBase<T>, IReadOnlySet<T>
{
    private readonly IReadOnlySet<T> _items;

    public PrintableReadOnlySet(IReadOnlySet<T> items) 
        : this(items, null)
    {
    }

    public PrintableReadOnlySet(
        IReadOnlySet<T> items,
        string? itemFormat = null,
        IFormatProvider? itemFormatProvider = null) 
        : base(items, itemFormat, itemFormatProvider)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public int Count => _items.Count;

    public bool Contains(T item) => _items.Contains(item);
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    public bool IsProperSubsetOf(IEnumerable<T> other) => _items.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => _items.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<T> other) => _items.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => _items.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<T> other) => _items.Overlaps(other);
    public bool SetEquals(IEnumerable<T> other) => _items.SetEquals(other);
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
}