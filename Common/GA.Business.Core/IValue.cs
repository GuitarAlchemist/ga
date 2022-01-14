namespace GA.Business.Core;

public interface IValue<TSelf> : IComparable<TSelf>, IComparable
    where TSelf : struct, IValue<TSelf>
{
    int IComparable<TSelf>.CompareTo(TSelf other) => Value.CompareTo(other.Value);
    int IComparable.CompareTo(object? obj)
    {
        if (ReferenceEquals(null, obj)) return 1;
        return obj is TSelf other ? CompareTo(other) : throw new ArgumentException($"Object must be of type {nameof(TSelf)}");
    }

    /// <summary>
    /// Gets the minimum <see cref="TSelf"/>.
    /// </summary>
    public static abstract TSelf Min { get; }

    /// <summary>
    /// Gets the maximum <see cref="TSelf"/>.
    /// </summary>
    public static abstract TSelf Max { get; }

    public int Value { get; init; }
}