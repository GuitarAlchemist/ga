namespace GA.Core.Collections;

public sealed class PrintableReadOnlyCollection<T>(
        IReadOnlyCollection<T> items,
        string? itemFormat = null,
        IFormatProvider? itemFormatProvider = null,
        string? itemSeparator = " ")
    : PrintableBase<T>(items, itemFormat, itemFormatProvider, itemSeparator), IReadOnlyCollection<T>
    where T : notnull
{
    private readonly IReadOnlyCollection<T> _items = items ?? throw new ArgumentNullException(nameof(items));

    public PrintableReadOnlyCollection(
        IReadOnlyCollection<T> items,
        string? itemSeparator = " ") 
            : this(items, null, null, itemSeparator)
    {
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int Count => _items.Count;
}

