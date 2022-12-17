namespace GA.Business.Core.Extensions;

using Intervals.Primitives;
using GA.Core;

public class IntervalStructure : IReadOnlyList<Semitones>, 
                                 IValueObject, IEquatable<IntervalStructure>
{
    #region Equality Members

    public bool Equals(IntervalStructure? other) => Value == other?.Value;

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != typeof(IntervalStructure)) return false;
        return Equals((IntervalStructure) obj);
    }

    public override int GetHashCode() => Value;

    public static bool operator ==(IntervalStructure? left, IntervalStructure? right) => Equals(left, right);
    public static bool operator !=(IntervalStructure? left, IntervalStructure? right) => !Equals(left, right);

    #endregion
    
    #region Relational members

    public int CompareTo(IntervalStructure other) => Value.CompareTo(other.Value);
    public static bool operator <(IntervalStructure left, IntervalStructure right) => left.CompareTo(right) < 0;
    public static bool operator >(IntervalStructure left, IntervalStructure right) => left.CompareTo(right) > 0;
    public static bool operator <=(IntervalStructure left, IntervalStructure right) => left.CompareTo(right) <= 0;
    public static bool operator >=(IntervalStructure left, IntervalStructure right) => left.CompareTo(right) >= 0;

    #endregion


    #region IReadOnlyList<Semitones> Members

    public IEnumerator<Semitones> GetEnumerator() => _semitonesList.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _semitonesList).GetEnumerator();
    public int Count => _semitonesList.Count;
    public Semitones this[int index] => _semitonesList[index];
    
    #endregion

    private readonly IReadOnlyList<Semitones> _semitonesList;

    public IntervalStructure(IReadOnlyList<Semitones> semitonesList)
    {
        _semitonesList = semitonesList ?? throw new ArgumentNullException(nameof(semitonesList));
        Value = ToBase12Value(semitonesList);
    }

    public IntervalStructure(int value) 
        : this(FromBase12Value(value))
    {
    }

    public int Value { get; }

    public override string ToString() => string.Join(" ", _semitonesList);

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
}