namespace GA.Core;

public interface IIndexer<in TKey, out TValue>
{
    TValue this[TKey key] { get; }
}