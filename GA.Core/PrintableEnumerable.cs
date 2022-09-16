namespace GA.Core;

public class PrintableEnumerable<T> : IEnumerable<T>
{
    private readonly IEnumerable<T> _items;

    public PrintableEnumerable(IEnumerable<T> items)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => string.Join(" ", _items);
}