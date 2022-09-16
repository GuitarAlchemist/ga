namespace GA.Core;

public sealed class ReadOnlyItems<TItem> : IReadOnlyCollection<TItem>
    where TItem : class
{
    private readonly IEnumerable<TItem> _items;

    public ReadOnlyItems(
        IEnumerable<TItem> items,
        int count)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        Count = count;
    }

    public ReadOnlyItems(
        IReadOnlyCollection<TItem> items) 
        : this(items, items.Count)
    {
    }

    public IEnumerator<TItem> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
    public int Count { get; }
    public override string ToString() => string.Join(" ", _items.Select(value => value.ToString()));
}