namespace GA.Business.Core.Intervals.Primitives;

/// <summary>
/// A chromatic interval size expressed in semitones (From -12 octaves to +12 octaves) - <see href="https://en.wikipedia.org/wiki/Semitone"/>
/// </summary>
/// <remarks>
/// Implements <see cref="IRangeValueObject{Semitones}"/>
/// </remarks>
[PublicAPI]
public readonly record struct Semitones : IParsable<Semitones>,
                                          IRangeValueObject<Semitones>
{
    #region IParsable<Semitones>

    public static Semitones Parse(string s, IFormatProvider? provider)
    {
        if (!TryParse(s, provider, out var result)) throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
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

    public static int CheckRange(int value) => ValueObjectUtils<Semitones>.EnsureValueRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<Semitones>.EnsureValueRange(value, minValue, maxValue);

    public static implicit operator Semitones(int value) => new() { Value = value };
    public static implicit operator int(Semitones semitones) => semitones._value;

    public static Semitones operator !(Semitones semitones) => FromValue(-semitones.Value);
    public static Semitones operator ++(Semitones semitones) => FromValue(semitones.Value + 1);
    public static Semitones operator --(Semitones semitones) => FromValue(semitones.Value - 1);
    public static Semitones operator +(Semitones a, Semitones b) => FromValue(a.Value + b.Value);
    public static Semitones operator -(Semitones a, Semitones b) => FromValue(a.Value - b.Value);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}

