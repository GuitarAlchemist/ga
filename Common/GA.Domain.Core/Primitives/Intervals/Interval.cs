namespace GA.Domain.Core.Primitives.Intervals;

using Notes;

/// <summary>
///     Interval discriminated union
/// </summary>
/// <remarks>
///     Subclasses: <see cref="Chromatic" /> | <see cref="Diatonic.Simple" /> | <see cref="Diatonic.Compound" />
/// </remarks>
[PublicAPI]
public abstract partial record Interval : IComparable<Interval>, IComparable
{
    /// <summary>
    ///     Get the number of semitones for the current <see cref="Interval" />
    /// </summary>
    /// <returns>
    ///     The <see cref="Semitones" />
    /// </returns>
    public abstract Semitones Semitones { get; }

    #region IComparable<Interval> Members

    public int CompareTo(Interval? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        return other is null
            ? 1
            : Semitones.CompareTo(other.Semitones);
    }

    #endregion

    #region IComparable Members

    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (ReferenceEquals(this, obj))
        {
            return 0;
        }

        return obj is Interval other
            ? CompareTo(other)
            : throw new ArgumentException($"Object must be of type {nameof(Interval)}");
    }

    public static bool operator <(Interval? left, Interval? right) =>
        Comparer<Interval>.Default.Compare(left, right) < 0;

    public static bool operator >(Interval? left, Interval? right) =>
        Comparer<Interval>.Default.Compare(left, right) > 0;

    public static bool operator <=(Interval? left, Interval? right) =>
        Comparer<Interval>.Default.Compare(left, right) <= 0;

    public static bool operator >=(Interval? left, Interval? right) =>
        Comparer<Interval>.Default.Compare(left, right) >= 0;

    #endregion
}
