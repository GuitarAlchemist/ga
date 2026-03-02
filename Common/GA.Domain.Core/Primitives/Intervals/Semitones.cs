namespace GA.Domain.Core.Primitives.Intervals;

using GA.Core.Abstractions;

/// <summary>
///     A chromatic interval size expressed in semitones (From -12 octaves to +12 octaves) -
///     <see href="https://en.wikipedia.org/wiki/Semitone" />
/// </summary>
/// <remarks>
///     Implements <see cref="IRangeValueObject{TSelf}" />
/// </remarks>
[PublicAPI]
public readonly record struct Semitones : IParsable<Semitones>,
    IRangeValueObject<Semitones>
{
    private const int _minValue = -12 * 12; // -12 octaves, 12 semitones each
    private const int _maxValue = 12 * 12; // +12 octaves, 12 semitones each

    private readonly int _value;
    public static Semitones None => FromValue(0);
    public static Semitones Unison => FromValue(0);
    public static Semitones Semitone => FromValue(1);
    public static Semitones Tone => FromValue(2);
    public static Semitones Tritone => FromValue(6);
    public static Semitones MinorThird => FromValue(3);
    public static Semitones MajorThird => FromValue(4);
    public static Semitones PerfectFourth => FromValue(5);
    public static Semitones DiminishedFifth => FromValue(6);
    public static Semitones PerfectFifth => FromValue(7);
    public static Semitones AugmentedFifth => FromValue(8);
    public static Semitones MinorSixth => FromValue(8);
    public static Semitones MajorSixth => FromValue(9);
    public static Semitones MinorSeventh => FromValue(10);
    public static Semitones MajorSeventh => FromValue(11);
    public static Semitones MinorNinth => FromValue(13);
    public static Semitones MajorNinth => FromValue(14);
    public static Semitones AugmentedNinth => FromValue(15);
    public static Semitones PerfectEleventh => FromValue(17);
    public static Semitones MajorThirteenth => FromValue(21);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Semitones FromValue([ValueRange(_minValue, _maxValue)] int value) =>
        new() { Value = value };

    public static Semitones Min => FromValue(_minValue);
    public static Semitones Max => FromValue(_maxValue);

    public static implicit operator Semitones(int value) => new() { Value = value };

    public static implicit operator int(Semitones semitones) => semitones._value;

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    public static Semitones Octave(int octaveCount = 1) => FromValue(12 * octaveCount);

    public static int CheckRange(int value) => ValueObjectUtils<Semitones>.EnsureValueRange(value, _minValue, _maxValue);

    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<Semitones>.EnsureValueRange(value, minValue, maxValue);

    public static Semitones operator !(Semitones semitones) => FromValue(-semitones.Value);

    public static Semitones operator ++(Semitones semitones) => FromValue(semitones.Value + 1);

    public static Semitones operator --(Semitones semitones) => FromValue(semitones.Value - 1);

    public static Semitones operator +(Semitones a, Semitones b) => FromValue(a.Value + b.Value);

    public static Semitones operator -(Semitones a, Semitones b) => FromValue(a.Value - b.Value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();

    #region IParsable<Semitones>

    public static Semitones Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result))
        {
            throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
        }

        return result;
    }

    public static bool TryParse(string? s, IFormatProvider? provider, out Semitones result)
    {
        result = default;

        if (int.TryParse(s, out var value))
        {
            result = FromValue(value);
            return true;
        }

        switch (s)
        {
            case "H":
            case "S":
                result = FromValue(1);
                return true;
            case "T":
            case "W":
                result = FromValue(2);
                return true;
        }

        return false;
    }

    #endregion

    #region Relational members

    public int CompareTo(Semitones other) => _value.CompareTo(other._value);

    public static bool operator <(Semitones left, Semitones right) => left.CompareTo(right) < 0;

    public static bool operator >(Semitones left, Semitones right) => left.CompareTo(right) > 0;

    public static bool operator <=(Semitones left, Semitones right) => left.CompareTo(right) <= 0;

    public static bool operator >=(Semitones left, Semitones right) => left.CompareTo(right) >= 0;

    #endregion
}
