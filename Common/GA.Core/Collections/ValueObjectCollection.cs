namespace GA.Core.Collections;

[PublicAPI]
public class ValueObjectCollection<T> : IReadOnlyCollection<T>
    where T : IValueObject<T>, new()
{
    public static ValueObjectCollection<T> Create(int start, int count)
    {
        var items = GetItems(start, count);
        return new(items, items.Length);
    }

    public static ValueObjectCollection<T> CreateWithHead(T head, int start, int count)
    {
        var items = GetItemsWithHead(head, start, count);
        return new(items, items.Length);
    }

    public static ValueObjectCollection<T> Create()
    {
        var minValue = T.Min.Value;
        var maxValue = T.Max.Value;
        var count = maxValue - minValue + 1;
        return Create(minValue, count);
    }

    private readonly IEnumerable<T> _items;

    protected ValueObjectCollection(
        IEnumerable<T> items,
        int count)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        Count = count;
    }

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
    public int Count { get; }
    public override string ToString() => string.Join(" ", _items.Select(value => value.ToString()));

    private static ImmutableArray<T> GetItems(int start, int count) =>
        Enumerable.Range(start, count)
                  .Select(i => new T {Value = i})
                  .ToImmutableArray();

    private static ImmutableArray<T> GetItemsWithHead(T head, int start, int count) =>
        new[] {head}
            .Union(Enumerable.Range(start, count)
            .Select(i => new T {Value = i}))
            .ToImmutableArray();
}