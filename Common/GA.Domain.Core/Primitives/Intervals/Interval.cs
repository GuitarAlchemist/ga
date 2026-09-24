namespace GA.Domain.Core.Primitives.Intervals;

using Notes;

/// <summary>
///     Interval discriminated union (<see href="https://en.wikipedia.org/wiki/Interval_(music)" />).
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

    /// <summary>
    ///     Compares two intervals by semitones, then by name.
    /// </summary>
    /// <remarks>
    ///     The name tiebreaker keeps the ordering consistent with structural equality (<c>CompareTo</c> returns 0 only for
    ///     equal intervals): enharmonic intervals such as A1 and m2 span the same number of semitones but are not equal.
    /// </remarks>
    public int CompareTo(Interval? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (other is null)
        {
            return 1;
        }

        var semitonesComparison = Semitones.CompareTo(other.Semitones);
        if (semitonesComparison != 0)
        {
            return semitonesComparison;
        }

        return Equals(other)
            ? 0
            : string.CompareOrdinal($"{GetType().Name}:{this}", $"{other.GetType().Name}:{other}");
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
