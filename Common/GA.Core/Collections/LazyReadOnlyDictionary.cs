namespace GA.Core.Collections;

using System.Diagnostics.CodeAnalysis;

[PublicAPI]
public class LazyReadOnlyDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    where TKey : notnull
{
    private readonly Lazy<IReadOnlyDictionary<TKey, TValue>> _lazy;

    public LazyReadOnlyDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection)
    {
        ArgumentNullException.ThrowIfNull(collection);

        _lazy = new(() =>
        {
            var distinctCollection = collection.DistinctBy(pair => pair.Key).ToImmutableArray();
            return new Dictionary<TKey, TValue>(distinctCollection).ToImmutableDictionary();
        });
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return _lazy.Value.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_lazy.Value).GetEnumerator();
    }

    public int Count => _lazy.Value.Count;

    public bool ContainsKey(TKey key)
    {
        return _lazy.Value.ContainsKey(key);
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        return _lazy.Value.TryGetValue(key, out value);
    }

    public TValue this[TKey key]
    {
        get
        {
            try
            {
                var dict = _lazy.Value;
                if (dict.TryGetValue(key, out var value))
                {
                    return value;
                }
            }
#pragma warning disable CS0168 // Variable is declared but never used
            catch (Exception ex)
#pragma warning restore CS0168 // Variable is declared but never used
            {
                Debugger.Break();
                throw;
            }

            // Failure
            var msg = $"{typeof(TKey).Name} key not found: {key}";

            Debugger.Break();
            throw new KeyNotFoundException(msg);
        }
    }

    public IEnumerable<TKey> Keys => _lazy.Value.Keys;
    public IEnumerable<TValue> Values => _lazy.Value.Values;
}
