namespace GA.Business.Core.Intervals.Primitives;

using GA.Core;
using GA.Core.Collections;

/// <summary>
/// A chromatic interval size expressed in semitones (From -12 octaves to +12 octaves) - <see href="https://en.wikipedia.org/wiki/Semitone"/>
/// </summary>
/// <remarks>
/// Implements <see cref="IRangeValueObject{Semitones}"/>
/// </remarks>
[PublicAPI]
public readonly record struct Semitones : IRangeValueObject<Semitones>
{
    #region Relational members

    public int CompareTo(Semitones other) => _value.CompareTo(other._value);
    public static bool operator <(Semitones left, Semitones right) => left.CompareTo(right) < 0;
    public static bool operator >(Semitones left, Semitones right) => left.CompareTo(right) > 0;
    public static bool operator <=(Semitones left, Semitones right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Semitones left, Semitones right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = -12 * 12; // -12 octaves, 12 semitones each
    private const int _maxValue = 12 * 12; // +12 octaves, 12 semitones each
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Semitones FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Semitones Min => FromValue(_minValue);
    public static Semitones Max => FromValue(_maxValue);
    public static Semitones None => FromValue(0);
    public static Semitones Unison => FromValue(0);
    public static Semitones Semitone => FromValue(1);
    public static Semitones Tone => FromValue(2);
    public static Semitones Tritone => FromValue(6);
    public static Semitones Octave(int octaveCount = 1) => FromValue(12 * octaveCount);

    public static int CheckRange(int value) => ValueObjectUtils<Semitones>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<Semitones>.CheckRange(value, minValue, maxValue);

    public static implicit operator Semitones(int value) => new() { Value = value };
    public static implicit operator int(Semitones semitones) => semitones._value;

    public static Semitones operator !(Semitones semitones) => FromValue(-semitones.Value);
    public static Semitones operator ++(Semitones semitones) => FromValue(semitones.Value + 1);
    public static Semitones operator --(Semitones semitones) => FromValue(semitones.Value - 1);
    public static Semitones operator +(Semitones a, Semitones b) => FromValue(a.Value + b.Value);
    public static Semitones operator -(Semitones a, Semitones b) => FromValue(a.Value - b.Value);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public override string ToString() => Value.ToString();
}

