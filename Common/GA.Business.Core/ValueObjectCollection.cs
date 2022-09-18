namespace GA.Business.Core;

public class ValueObjectCollection<T> : IReadOnlyCollection<T>
    where T : struct, IValueObject<T>
{
    public static ValueObjectCollection<T> Create(int start, int count)
    {
        var items = GetItems(start, count);
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

    // ReSharper disable once InconsistentNaming
    private static ImmutableArray<T> GetItems(int start, int count)
    {
        var items =
            Enumerable.Range(start, count)
                .Select(i => new T {Value = i})
                .ToImmutableArray();
        return items;
    }
}