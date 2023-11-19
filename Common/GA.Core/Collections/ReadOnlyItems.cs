namespace GA.Core.Collections;

public sealed class ReadOnlyItems<TItem>(IEnumerable<TItem> items, int count) : IReadOnlyCollection<TItem>
    where TItem : class
{
    private readonly IEnumerable<TItem> _items = items ?? throw new ArgumentNullException(nameof(items));

    public ReadOnlyItems(
        IReadOnlyCollection<TItem> items) 
        : this(items, items.Count)
    {
    }

    public IEnumerator<TItem> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
    public int Count { get; } = count;
    public override string ToString() => string.Join(" ", _items.Select(value => value.ToString()));
}