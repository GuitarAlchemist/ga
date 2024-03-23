namespace GA.Business.Core.Atonal.Primitives;

using GA.Business.Core.Intervals.Primitives;

/// <summary>
/// An interval class (Between <see cref="Min" /> and <see cref="Max" />)
/// IC   Included intervals     Tonal CounterPart
/// 0    0                      Unison and octave
/// 1    1 and 11               Minor 2nd and major 7th
/// 2    2 and 10               Major 2nd and minor 7th
/// 3    3 and 9                Minor 3rd and major 6th
/// 4    4 and 8                Major 3rd and minor 6th
/// 5    5 and 7                Perfect 4th and Perfect 5th
/// 6    6                      Augmented 4th and Diminished 5th
/// </summary>
/// <remarks>
/// See https://en.wikipedia.org/wiki/Interval_class
/// http://www.jaytomlin.com/music/settheory/help.html
///
/// Implements <see cref="IStaticValueObjectList{IntervalClass}"/>
/// </remarks>
[PublicAPI]
public readonly record struct IntervalClass : IStaticValueObjectList<IntervalClass>
{
    #region IStaticValueObjectList<IntervalClass> Members

    public static IReadOnlyCollection<IntervalClass> Items => ValueObjectUtils<IntervalClass>.Items;
    public static IReadOnlyList<int> Values => ValueObjectUtils<IntervalClass>.Values;
   
    #endregion

    #region IValueObject<IntervalClass>

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    #endregion

    #region Relational members

    public int CompareTo(IntervalClass other) => _value.CompareTo(other._value);
    public static bool operator <(IntervalClass left, IntervalClass right) => left.CompareTo(right) < 0;
    public static bool operator >(IntervalClass left, IntervalClass right) => left.CompareTo(right) > 0;
    public static bool operator <=(IntervalClass left, IntervalClass right) => left.CompareTo(right) <= 0;
    public static bool operator >=(IntervalClass left, IntervalClass right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 0;
    private const int _maxValue = 6;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntervalClass FromValue(int value)
    {
        var inOctaveValue = Math.Abs(value) % 12; // Apply octave equivalence
        var normalizedValue = inOctaveValue > _maxValue ? 12 - value : value; // Apply interval inversion equivalence
        return new() {Value = normalizedValue};
    }

    public static IntervalClass FromSemitones(Semitones semitones) => FromValue(semitones.Value);
    public static IReadOnlyCollection<IntervalClass> Range(int start, int count) => ValueObjectUtils<IntervalClass>.GetItems(start, count);

    public static IntervalClass Min => FromValue(_minValue);
    public static IntervalClass Max => FromValue(_maxValue);
    public static IntervalClass Hemitone => FromValue(1);
    public static IntervalClass Tone=> FromValue(2);
    public static IntervalClass Tritone => FromValue(6);

    public static int CheckRange(int value) => IRangeValueObject<IntervalClass>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<IntervalClass>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator IntervalClass(int value) => new() { Value = value };
    public static implicit operator int(IntervalClass ic) => ic.Value;

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<IntervalClass>.CheckRange(Value, _minValue, maxValue);

    public override string ToString() => _value switch
    {
        0 => "0 (Unison)",
        1 => "1 (m2, M7)",
        2 => "2 (M2, m7)",
        3 => "3 (m3, M6)",
        4 => "4 (M3, m6)",
        5 => "5 (P4, P5)",
        6 => "6 (A4, d5; Tritone)",
        _ => throw new InvalidOperationException()
    };
}