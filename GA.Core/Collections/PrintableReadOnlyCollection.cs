namespace GA.Core.Collections;

public sealed class PrintableReadOnlyCollection<T> : PrintableBase<T>, IReadOnlyCollection<T>
    where T : notnull
{
    private readonly IReadOnlyCollection<T> _items;

    public PrintableReadOnlyCollection(
        IReadOnlyCollection<T> items) 
            : this(items, null)
    {
    }

    public PrintableReadOnlyCollection(
        IReadOnlyCollection<T> items,
        string? itemFormat = null,
        IFormatProvider? itemFormatProvider = null) 
            : base(items, itemFormat, itemFormatProvider)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _items.Count;
}

