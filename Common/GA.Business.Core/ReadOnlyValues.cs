namespace GA.Business.Core;

using System.Collections;

public sealed class ReadOnlyValues<TValue> : IReadOnlyCollection<TValue>
    where TValue : struct, IValue<TValue>
{
    public static ReadOnlyValues<TValue> Create(int start, int count)
    {
        var collection =
            Enumerable.Range(start, count)
                .Select(i => new TValue { Value = i });

        var result = new ReadOnlyValues<TValue>(collection, count);

        return result;
    }

    public static ReadOnlyValues<TValue> Create()
    {
        var minValue = TValue.Min.Value;
        var maxValue = TValue.Max.Value;
        var count = maxValue - minValue + 1;
        var collection =
            Enumerable.Range(minValue, count)
                .Select(i => new TValue { Value = i });

        var result = new ReadOnlyValues<TValue>(collection, count);

        return result;
    }

    private readonly IEnumerable<TValue> _items;

    public ReadOnlyValues(
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
}