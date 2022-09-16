using System.Diagnostics;

namespace GA.Core;

using System.Collections.Immutable;
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

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>_lazy.Value.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()=> ((IEnumerable) _lazy.Value).GetEnumerator();
    public int Count => _lazy.Value.Count;
    public bool ContainsKey(TKey key) => _lazy.Value.ContainsKey(key);
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => _lazy.Value.TryGetValue(key, out value);
    public TValue this[TKey key]
    {
        get
        {
            try
            {
                var dict = _lazy.Value;
                if (dict.TryGetValue(key, out var value)) return value;
            }
            catch (Exception ex)
            {
                Debugger.Break();
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


