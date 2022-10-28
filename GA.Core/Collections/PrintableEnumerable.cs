namespace GA.Core.Collections;

public sealed class PrintableEnumerable<T> : PrintableBase<T>, IEnumerable<T>
{
    private readonly IEnumerable<T> _items;

    public PrintableEnumerable(IEnumerable<T> items) 
        : base(items)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}