namespace GA.Core.Collections;

[PublicAPI]
public abstract class PrintableImmutableSet<T>(ImmutableSortedSet<T> items) : LazyPrintableCollectionBase<T>(items),
    IImmutableSet<T>
    where T : notnull
{
    protected static ImmutableSortedSet<T> GetSet(IEnumerable<T> items)
    {
        if (items is ImmutableSortedSet<T> set)
        {
            return set;
        }

        return items.ToImmutableSortedSet();
    }

    #region IImmutableSet<T> Members

    public IImmutableSet<T> Add(T value)
    {
        return items.Add(value);
    }

    public IImmutableSet<T> Clear()
    {
        return items.Clear();
    }

    public bool Contains(T value)
    {
        return items.Contains(value);
    }

    public IImmutableSet<T> Except(IEnumerable<T> other)
    {
        return items.Except(other);
    }

    public IImmutableSet<T> Intersect(IEnumerable<T> other)
    {
        return items.Intersect(other);
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        return items.IsProperSubsetOf(other);
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        return items.IsProperSupersetOf(other);
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        return items.IsSubsetOf(other);
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        return items.IsSupersetOf(other);
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        return items.Overlaps(other);
    }

    public IImmutableSet<T> Remove(T value)
    {
        return items.Remove(value);
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        return items.SetEquals(other);
    }

    public IImmutableSet<T> SymmetricExcept(IEnumerable<T> other)
    {
        return items.SymmetricExcept(other);
    }

    public bool TryGetValue(T equalValue, out T actualValue)
    {
        return items.TryGetValue(equalValue, out actualValue);
    }

    public IImmutableSet<T> Union(IEnumerable<T> other)
    {
        return items.Union(other);
    }

    #endregion
}
