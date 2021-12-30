namespace GA.Business.Core;

using System.Collections;

public sealed class ReadOnlyCollectionWrapper<TValue> : IReadOnlyCollection<TValue>
    where TValue : struct, IValue<TValue>
{
    public static ReadOnlyCollectionWrapper<TValue> Create(int start, int count)
    {
        var minValue = start;
        var maxValue = start + count - 1;
        // ValueUtils<TValue>.CheckRange(minValue);
        // ValueUtils<TValue>.CheckRange(maxValue);

        var collection =
            Enumerable.Range(minValue, count)
                .Select(i => new TValue { Value = i });

        var result = new ReadOnlyCollectionWrapper<TValue>(collection, count);

        return result;

    }

    public static ReadOnlyCollectionWrapper<TValue> Create()
    {
        var minValue = TValue.Min.Value;
        var maxValue = TValue.Max.Value;
        var count = maxValue - minValue;
        var collection =
            Enumerable.Range(minValue, count)
                .Select(i => new TValue { Value = i });

        var result = new ReadOnlyCollectionWrapper<TValue>(collection, count);

        return result;
    }

    private readonly IEnumerable<TValue> _items;

    public ReadOnlyCollectionWrapper(
        IEnumerable<TValue> items,
        int count)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        Count = count;
    }

    public IEnumerator<TValue> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_items).GetEnumerator();
    public int Count { get; }
}