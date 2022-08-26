namespace GA.Business.Core;

using System.Collections;
using System.Collections.Immutable;

public class ValueObjectCollection<TValue> : IReadOnlyCollection<TValue>
    where TValue : struct, IValueObject<TValue>
{
    public static ValueObjectCollection<TValue> Create(int start, int count)
    {
        var items = GetItems(start, count);
        return new(items, items.Length);
    }

    public static ValueObjectCollection<TValue> Create()
    {
        var minValue = TValue.Min.Value;
        var maxValue = TValue.Max.Value;
        var count = maxValue - minValue + 1;
        return Create(minValue, count);
    }

    private readonly IEnumerable<TValue> _items;

    protected ValueObjectCollection(
        IEnumerable<TValue> items,
        int count)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        Count = count;
    }

    public IEnumerator<TValue> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
    public int Count { get; }
    public override string ToString() => string.Join(" ", _items.Select(value => value.ToString()));

    private static ImmutableArray<TValue> GetItems(int start, int count)
    {
        var items =
            Enumerable.Range(start, count)
                .Select(i => new TValue {Value = i})
                .ToImmutableArray();
        return items;
    }
}