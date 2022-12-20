namespace GA.Business.Core.Extensions;

using Intervals.Primitives;
using GA.Core;

/// <summary>
/// The list of semitones distance between the notes of a scale. (e.g. Major Scale: 2, 2, 1, 2, 2, 2, 1)
/// </summary>
public class IntervalPattern : IReadOnlyList<Semitones>, 
                               IValueObject, IEquatable<IntervalPattern>
{
    #region Equality Members

    public bool Equals(IntervalPattern? other) => Value == other?.Value;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != typeof(IntervalPattern)) return false;
        return Equals((IntervalPattern) obj);
    }

    public override int GetHashCode() => Value;

    public static bool operator ==(IntervalPattern? left, IntervalPattern? right) => Equals(left, right);
    public static bool operator !=(IntervalPattern? left, IntervalPattern? right) => !Equals(left, right);

    #endregion
    
    #region Relational members

    public int CompareTo(IntervalPattern other) => Value.CompareTo(other.Value);
    public static bool operator <(IntervalPattern left, IntervalPattern right) => left.CompareTo(right) < 0;
    public static bool operator >(IntervalPattern left, IntervalPattern right) => left.CompareTo(right) > 0;
    public static bool operator <=(IntervalPattern left, IntervalPattern right) => left.CompareTo(right) <= 0;
    public static bool operator >=(IntervalPattern left, IntervalPattern right) => left.CompareTo(right) >= 0;

    #endregion


    #region IReadOnlyList<Semitones> Members

    public IEnumerator<Semitones> GetEnumerator() => _semitonesList.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _semitonesList).GetEnumerator();
    public int Count => _semitonesList.Count;
    public Semitones this[int index] => _semitonesList[index];
    
    #endregion

    private readonly IReadOnlyList<Semitones> _semitonesList;

    public IntervalPattern(IReadOnlyList<Semitones> semitonesList)
    {
        _semitonesList = semitonesList ?? throw new ArgumentNullException(nameof(semitonesList));
        if (semitonesList.Sum(semitones => semitones.Value) != 12) throw new InvalidEnumArgumentException($"Invalid {semitonesList} parameter. The sum of semitones must be equal to 12");
        Value = ToBase12Value(semitonesList);
    }

    public IntervalPattern(int value) 
        : this(FromBase12Value(value))
    {
    }

    public int Value { get; }

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('[');
        sb.Append(string.Join(" ", _semitonesList));
        sb.Append(']');
        return sb.ToString();
    }

    private static IntervalPattern FromBase12Value(int value)
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
}