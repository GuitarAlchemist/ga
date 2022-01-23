namespace GA.Core;

public abstract class LazyIndexerBase<TKey, TValue>
    where TKey : notnull
{
    public TValue this[TKey key] => _lazyDictionary[key];

    private readonly LazyReadOnlyDictionary<TKey, TValue> _lazyDictionary;

    protected LazyIndexerBase(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
    {
        _lazyDictionary = new(keyValuePairs);
    }
}