namespace GA.Core.Combinatorics;

public record OrderedPair<T>(T Item1, T Item2);

public record UnorderedPair<T>(T Item1, T Item2)
{
    public virtual bool Equals(UnorderedPair<T>? other) =>
        other != null
        &&
        (
            (EqualityComparer<T>.Default.Equals(Item1, other.Item1) &&
             EqualityComparer<T>.Default.Equals(Item2, other.Item2))
            ||
            (EqualityComparer<T>.Default.Equals(Item1, other.Item2) &&
             EqualityComparer<T>.Default.Equals(Item2, other.Item1))
        );

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash1 = Item1?.GetHashCode() ?? 0;
        var hash2 = Item2?.GetHashCode() ?? 0;

        return HashCode.Combine(Math.Min(hash1, hash2), Math.Max(hash1, hash2));
    }

    /// <inheritdoc />
    public override string ToString() => $"({Item1}, {Item2})";
}

public readonly record struct UnorderedPairStruct<T>(T Item1, T Item2)
{
    /// <remarks>
    ///     Declared with a non-nullable parameter so that it replaces the compiler-generated (order-sensitive)
    ///     implementation - otherwise <c>==</c> and hash-based lookups would ignore the unordered semantics.
    /// </remarks>
    public bool Equals(UnorderedPairStruct<T> other) =>
        (EqualityComparer<T>.Default.Equals(Item1, other.Item1) &&
         EqualityComparer<T>.Default.Equals(Item2, other.Item2))
        ||
        (EqualityComparer<T>.Default.Equals(Item1, other.Item2) &&
         EqualityComparer<T>.Default.Equals(Item2, other.Item1));

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash1 = Item1?.GetHashCode() ?? 0;
        var hash2 = Item2?.GetHashCode() ?? 0;

        return HashCode.Combine(Math.Min(hash1, hash2), Math.Max(hash1, hash2));
    }

    /// <inheritdoc />
    public override string ToString() => $"({Item1}, {Item2})";
}
