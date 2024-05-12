namespace GA.Core.Collections;

[PublicAPI]
public abstract class PrintableImmutableSet<T>(ImmutableSortedSet<T> items) : LazyPrintableCollectionBase<T>(items), 
                                                                              IImmutableSet<T> 
    where T : notnull
{
    #region IImmutableSet<T> Members

    public IImmutableSet<T> Add(T value) => items.Add(value);
    public IImmutableSet<T> Clear() => items.Clear();
    public bool Contains(T value) => items.Contains(value);
    public IImmutableSet<T> Except(IEnumerable<T> other) => items.Except(other);
    public IImmutableSet<T> Intersect(IEnumerable<T> other) => items.Intersect(other);
    public bool IsProperSubsetOf(IEnumerable<T> other) => items.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<T> other) => items.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<T> other) => items.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<T> other) => items.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<T> other) => items.Overlaps(other);
    public IImmutableSet<T> Remove(T value) => items.Remove(value);
    public bool SetEquals(IEnumerable<T> other) => items.SetEquals(other);
    public IImmutableSet<T> SymmetricExcept(IEnumerable<T> other) => items.SymmetricExcept(other);
    public bool TryGetValue(T equalValue, out T actualValue) => items.TryGetValue(equalValue, out actualValue);
    public IImmutableSet<T> Union(IEnumerable<T> other) => items.Union(other);    

    #endregion
    
    protected static ImmutableSortedSet<T> GetSet(IEnumerable<T> items)
    {
        if (items is ImmutableSortedSet<T> set) return set;
        return items.ToImmutableSortedSet();
    }    
}