namespace GA.Core.Combinatorics;

using System.Collections.Immutable;
using System.Numerics;
using Collections;

/// <summary>
/// Arrange collection items into all possible arrays (Collection items can be used multiples times) - Also called "k-tuple"
/// </summary>
/// <typeparam name="T"></typeparam>
/// <remarks>
/// Inspired from https://weibeld.net/math/combinatorics.html#variations-with-repetitions-k-tuples
/// 
/// Arrangement of k elements from a set of n elements into a sequence of length k, where each element of the set may be chosen multiple times.
/// 
/// Known as k-tuples in the English literature.
/// k &lt; n or k = n or k &gt; n
/// In many cases, the elements of the set can be thought of as classes from which multiple objects can be instantiated.
/// Can also be seen as forming words of length k over an alphabet of size n
///
/// Example:
///
/// Set: {a,b,c}
/// n=3
/// k=2
/// 9 words: (a,a),(a,b),(a,c),(b,a),(b,b),(b,c),(c,a),(c,b),(c,c)
/// 
/// </remarks>
public class VariationsWithRepetitions<T> : IIndexer<BigInteger, ImmutableArray<T>>,
                                            IIndexer<IReadOnlyCollection<T>, BigInteger>,
                                            IEnumerable<ImmutableArray<T>>
    where T : notnull
{
    private readonly BigInteger _base;
    private readonly int _length;
    private readonly ImmutableDictionary<T, int> _indexByItem;
    private readonly ImmutableDictionary<int, T> _itemByIndex;

    public VariationsWithRepetitions(
        IReadOnlyCollection<T> collection,
        int length)
    {
        _base = new(collection.Count);
        Count = BigInteger.Pow(_base, length);
        _length = length;
        
        _indexByItem =
            collection.Select((o, i) => (o, i))
                .ToImmutableDictionary(
                    tuple => tuple.o,
                    tuple => tuple.i);
        _itemByIndex =
            collection.Select((o, i) => (o, i))
                .ToImmutableDictionary(
                    tuple => tuple.i,
                    tuple => tuple.o);
    }

    public BigInteger Count { get; }
    public ImmutableArray<T> this[BigInteger key] => GetArray(key);
    public BigInteger this[IReadOnlyCollection<T> items] => GetIdentity(items);

    private BigInteger GetIdentity(IEnumerable<T> items)
    {
        // id = index(item0) + index(item1) * _base ^ 1 + index(item2) * _base ^ 2 + etc...
        // id = Sum(index(itemX * _base ^ X); X: 0..count(items)
        var result = new BigInteger(0);
        var weight = new BigInteger(1);
        foreach (var item in items)
        {
            var value = _indexByItem[item];
            result += value * weight;
            weight *=  _base;
        }
        return result;
    }

    private ImmutableArray<T> GetArray(BigInteger id)
    {
        var items = new T[_length];
        for (var i = 0; i < _length; i++)
        {
            var itemIndex = (int) BigInteger.Remainder(id, _base);
            items[i] = _itemByIndex[itemIndex];
            id = BigInteger.Divide(id, _base);
        }
        return items.ToImmutableArray();
    }

    public IEnumerator<ImmutableArray<T>> GetEnumerator()
    {
        var id = BigInteger.Zero;
        while (id.CompareTo(Count) != 0)
        {
            yield return GetArray(id++);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}