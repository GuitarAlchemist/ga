namespace GA.Business.Core.Extensions;

using Intervals.Primitives;

/// <summary>
/// The list of semitones distance between the notes of a scale. (e.g. Major Scale: 2, 2, 1, 2, 2, 2, 1)
/// </summary>
/// <remarks>
/// Implements <see cref="IReadOnlyList{Semitones}"/>, <see cref="IValueObject"/>, <see cref="IEquatable{ScaleIntervalPattern}"/>
/// </remarks>
public class ScaleIntervalPattern : IReadOnlyList<Semitones>, 
                                    IValueObject, 
                                    IEquatable<ScaleIntervalPattern>
{
    #region Equality Members

    public bool Equals(ScaleIntervalPattern? other) => Value == other?.Value;

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == typeof(ScaleIntervalPattern) && Equals((ScaleIntervalPattern) obj);
    }

    public override int GetHashCode() => Value;

    public static bool operator ==(ScaleIntervalPattern? left, ScaleIntervalPattern? right) => Equals(left, right);
    public static bool operator !=(ScaleIntervalPattern? left, ScaleIntervalPattern? right) => !Equals(left, right);

    #endregion
    
    #region Relational members

    public int CompareTo(ScaleIntervalPattern other) => Value.CompareTo(other.Value);
    public static bool operator <(ScaleIntervalPattern left, ScaleIntervalPattern right) => left.CompareTo(right) < 0;
    public static bool operator >(ScaleIntervalPattern left, ScaleIntervalPattern right) => left.CompareTo(right) > 0;
    public static bool operator <=(ScaleIntervalPattern left, ScaleIntervalPattern right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ScaleIntervalPattern left, ScaleIntervalPattern right) => left.CompareTo(right) >= 0;

    #endregion

    #region IReadOnlyList<Semitones> Members

    public IEnumerator<Semitones> GetEnumerator() => _semitonesList.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _semitonesList).GetEnumerator();
    public int Count => _semitonesList.Count;
    public Semitones this[int index] => _semitonesList[index];
    
    #endregion

    private readonly IReadOnlyList<Semitones> _semitonesList;

    /// <summary>
    /// Creates an interval pattern
    /// </summary>
    /// <param name="semitoneCollection">The <see cref="IEnumerable{Semitones}"/></param>
    /// <exception cref="InvalidEnumArgumentException">Thrown when the sum of semitone distance is not equal to 12</exception>
    public ScaleIntervalPattern(IEnumerable<Semitones> semitoneCollection)
    {
        _semitonesList = semitoneCollection.ToImmutableList();
        if (_semitonesList.Sum(semitones => semitones.Value) != 12) throw new InvalidEnumArgumentException($"Invalid {nameof(semitoneCollection)} parameter. The sum of semitones must be equal to 12");
        Value = ToBase12Value(_semitonesList);
    }

    public ScaleIntervalPattern(int value) 
        : this(FromBase12Value(value))
    {
    }

    /// <summary>
    /// Gets an <see cref="int"/> value that represents the scale interval pattern
    /// </summary>
    public int Value { get; }

    public override string ToString() => $"[{string.Join(" ", _semitonesList)}]";

    private static ScaleIntervalPattern FromBase12Value(int value)
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