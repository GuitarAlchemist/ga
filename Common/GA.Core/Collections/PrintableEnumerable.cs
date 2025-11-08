namespace GA.Core.Collections;

public sealed class PrintableEnumerable<T>(
    IEnumerable<T> items,
    string? itemSeparator = " ") : PrintableBase<T>(items, itemSeparator: itemSeparator), IEnumerable<T>
{
    private readonly IEnumerable<T> _items = items ?? throw new ArgumentNullException(nameof(items));

    public IEnumerator<T> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
