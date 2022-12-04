namespace GA.Core.Collections;

public interface IIndexer<in TKey, out TValue>
{
    TValue this[TKey key] { get; }
}