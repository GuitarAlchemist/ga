namespace GA.Core.Collections;

public abstract class LazyIndexerBase<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
    where TKey : notnull
{
    public TValue this[TKey key] => _lazyDictionary[key];
    public IReadOnlyDictionary<TKey, TValue> Dictionary => _lazyDictionary;

    private readonly LazyReadOnlyDictionary<TKey, TValue> _lazyDictionary = new(keyValuePairs);
}