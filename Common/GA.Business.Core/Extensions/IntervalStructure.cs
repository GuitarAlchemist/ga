namespace GA.Business.Core.Extensions;

using Intervals;
using Intervals.Primitives;

/// <summary>
///     The list of semitones distance between a series of notes (e.g. Major Scale: 2, 2, 1, 2, 2, 2, 1)
/// </summary>
/// <remarks>
///     Implements <see cref="IReadOnlyList{Semitones}" />, <see cref="IValueObject" />,
///     <see cref="IEquatable{ScaleIntervalPattern}" />
/// </remarks>
public class IntervalStructure : IParsable<IntervalStructure>,
    IReadOnlyList<Semitones>,
    IValueObject,
    IEquatable<IntervalStructure>
{
    private readonly IReadOnlyList<Semitones> _semitonesList;

    /// <summary>
    ///     Creates an interval structure instance from a collection of semitones
    /// </summary>
    /// <param name="semitoneCollection">The <see cref="IEnumerable{Semitones}" /></param>
    /// <exception cref="InvalidEnumArgumentException">Thrown when the sum of semitone distance is not equal to 12</exception>
    public IntervalStructure(IEnumerable<Semitones> semitoneCollection)
    {
        _semitonesList = semitoneCollection as IReadOnlyList<Semitones> ?? [.. semitoneCollection];
        if (_semitonesList.Sum(semitones => semitones.Value) != 12)
        {
            throw new InvalidEnumArgumentException(
                $"Invalid {nameof(semitoneCollection)} parameter. The sum of semitones must be equal to 12");
        }

        Value = ToBase12Value(_semitonesList);
    }

    /// <summary>
    ///     Creates an interval structure instance from a base-12 value
    /// </summary>
    /// <param name="value"></param>
    public IntervalStructure(int value)
        : this(FromBase12Value(value))
    {
    }

    /// <summary>
    ///     Gets the intervals from root
    /// </summary>
    /// <returns>The <see cref="ChromaticIntervalsFromRoot" /></returns>
    public ChromaticIntervalsFromRoot IntervalsFromRoot => GetIntervalsFromRoot(_semitonesList);

    /// <summary>
    ///     Gets a <see cref="int" /> base-12 value that represents the scale interval pattern
    /// </summary>
    public int Value { get; }

    public override string ToString()
    {
        return $"[{string.Join(" ", _semitonesList)}]";
    }

    private static IntervalStructure FromBase12Value(int value)
    {
        var semitonesListBuilder = ImmutableList.CreateBuilder<Semitones>();
        var dividend = value;

        while (dividend > 0)
        {
            var remainder = dividend % 12;
            semitonesListBuilder.Add(remainder);
            dividend /= 12;
        }

        return new(semitonesListBuilder.ToImmutable());
    }

    private static int ToBase12Value(IEnumerable<Semitones> semitonesCollection)
    {
        var weight = 1;
        var value = 0;
        foreach (var semitone in semitonesCollection)
        {
            value += semitone.Value * weight;
            weight *= 12;
        }

        return value;
    }

    private static ChromaticIntervalsFromRoot GetIntervalsFromRoot(IEnumerable<Semitones> intervalStructureSemitones)
    {
        var builder = ImmutableArray.CreateBuilder<Interval.Chromatic>();
        Interval.Chromatic cumulativeInterval = 0;
        builder.Add(cumulativeInterval);
        foreach (var intervalStructureSemitone in intervalStructureSemitones)
        {
            cumulativeInterval += intervalStructureSemitone.Value;
            builder.Add(cumulativeInterval);
        }

        return new(builder.ToImmutable());
    }

    #region IParsable<IntervalStructure> Members

    public static IntervalStructure Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        }

        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out IntervalStructure result)
    {
        ArgumentNullException.ThrowIfNull(s);

        result = null!;
        var segments = s.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries).Select(segment => segment.Trim());
        var list = new List<Semitones>();
        foreach (var segment in segments)
        {
            if (!Semitones.TryParse(segment, null, out var pitchClass))
            {
                return false; // Fail if one item fails parsing
            }

            list.Add(pitchClass);
        }

        // Success
        result = new(list);
        return true;
    }

    #endregion

    #region Equality Members

    public bool Equals(IntervalStructure? other)
    {
        return Value == other?.Value;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == typeof(IntervalStructure) && Equals((IntervalStructure)obj);
    }

    public override int GetHashCode()
    {
        return Value;
    }

    public static bool operator ==(IntervalStructure? left, IntervalStructure? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(IntervalStructure? left, IntervalStructure? right)
    {
        return !Equals(left, right);
    }

    #endregion

    #region Relational members

    public int CompareTo(IntervalStructure other)
    {
        return Value.CompareTo(other.Value);
    }

    public static bool operator <(IntervalStructure left, IntervalStructure right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(IntervalStructure left, IntervalStructure right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(IntervalStructure left, IntervalStructure right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(IntervalStructure left, IntervalStructure right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion

    #region IReadOnlyList<Semitones> Members

    public IEnumerator<Semitones> GetEnumerator()
    {
        return _semitonesList.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_semitonesList).GetEnumerator();
    }

    public int Count => _semitonesList.Count;
    public Semitones this[int index] => _semitonesList[index];

    #endregion
}
