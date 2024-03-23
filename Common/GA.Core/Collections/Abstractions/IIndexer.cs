namespace GA.Core.Collections.Abstractions;

public interface IIndexer<in TKey, out TValue>
{
    TValue this[TKey key] { get; }
}