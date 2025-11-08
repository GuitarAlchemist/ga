namespace GA.Core;

public record OrderedPair<T>(T Item1, T Item2);

public record UnorderedPair<T>(T Item1, T Item2)
{
    public virtual bool Equals(UnorderedPair<T>? other)
    {
        return other != null
               &&
               (
                   (EqualityComparer<T>.Default.Equals(Item1, other.Item1) &&
                    EqualityComparer<T>.Default.Equals(Item2, other.Item2))
                   ||
                   (EqualityComparer<T>.Default.Equals(Item1, other.Item2) &&
                    EqualityComparer<T>.Default.Equals(Item2, other.Item1))
               );
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash1 = Item1?.GetHashCode() ?? 0;
        var hash2 = Item2?.GetHashCode() ?? 0;

        return unchecked(hash1 + hash2);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"({Item1}, {Item2})";
    }
}

public readonly record struct UnorderedPairStruct<T>(T Item1, T Item2)
{
    public bool Equals(UnorderedPairStruct<T>? other)
    {
        return other != null
               &&
               (
                   (EqualityComparer<T>.Default.Equals(Item1, other.Value.Item1) &&
                    EqualityComparer<T>.Default.Equals(Item2, other.Value.Item2))
                   ||
                   (EqualityComparer<T>.Default.Equals(Item1, other.Value.Item2) &&
                    EqualityComparer<T>.Default.Equals(Item2, other.Value.Item1))
               );
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash1 = Item1?.GetHashCode() ?? 0;
        var hash2 = Item2?.GetHashCode() ?? 0;

        return unchecked(hash1 + hash2);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"({Item1}, {Item2})";
    }
}
