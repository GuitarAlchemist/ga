namespace GA.Core.Collections;

public class Indexer<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary) : IIndexer<TKey, TValue>
{
    private readonly IReadOnlyDictionary<TKey, TValue> _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));

    public TValue this[TKey key] => _dictionary[key];
}