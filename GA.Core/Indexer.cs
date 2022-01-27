namespace GA.Core;

public class Indexer<TKey, TValue> : IIndexer<TKey, TValue>
{
    private readonly IReadOnlyDictionary<TKey, TValue> _dictionary;

    public Indexer(IReadOnlyDictionary<TKey, TValue> dictionary)
    {
        _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
    }

    public TValue this[TKey key] => _dictionary[key];
}