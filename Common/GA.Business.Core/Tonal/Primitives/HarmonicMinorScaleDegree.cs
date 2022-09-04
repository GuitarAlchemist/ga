namespace GA.Business.Core.Tonal.Primitives;

using Modes;

/// <inheritdoc cref="IEquatable{HarmonicMinorScaleDegree}" />
/// <inheritdoc cref="IComparable{HarmonicMinorScaleDegree}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An music minor scale degree - See https://en.wikipedia.org/wiki/Degree_(music)
/// </summary>
[PublicAPI]
public readonly record struct HarmonicMinorScaleDegree : IMinorScaleModeDegree<HarmonicMinorScaleDegree>
{
    #region Relational members

    public int CompareTo(HarmonicMinorScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(HarmonicMinorScaleDegree left, HarmonicMinorScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(HarmonicMinorScaleDegree left, HarmonicMinorScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(HarmonicMinorScaleDegree left, HarmonicMinorScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(HarmonicMinorScaleDegree left, HarmonicMinorScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HarmonicMinorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static HarmonicMinorScaleDegree Min => FromValue(_minValue);
    public static HarmonicMinorScaleDegree Max => FromValue(_maxValue);
    public static HarmonicMinorScaleDegree HarmonicMinor => new() {Value = 1};
    public static HarmonicMinorScaleDegree LocrianNaturalSixth => new() {Value = 2};
    public static HarmonicMinorScaleDegree IonianAugmented => new() {Value = 3};
    public static HarmonicMinorScaleDegree DorianSharpFourth => new() {Value = 4};
    public static HarmonicMinorScaleDegree PhrygianDominant => new() {Value = 5};
    public static HarmonicMinorScaleDegree LydianSharpSecond => new() {Value = 6};
    public static HarmonicMinorScaleDegree Alteredd7 => new() {Value = 7};

    public static int CheckRange(int value) => IValueObject<HarmonicMinorScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IValueObject<HarmonicMinorScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator HarmonicMinorScaleDegree(int value) => FromValue(value);
    public static implicit operator int(HarmonicMinorScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<HarmonicMinorScaleDegree> All => IValueObject<HarmonicMinorScaleDegree>.GetRange(_minValue, _maxValue - _minValue);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public override string ToString() => Value.ToString();

    public ScaleDegreeFunction ToFunction() => _value switch
    {
        1 => ScaleDegreeFunction.Tonic,
        2 => ScaleDegreeFunction.Supertonic,
        3 => ScaleDegreeFunction.Mediant,
        4 => ScaleDegreeFunction.Subdominant,
        5 => ScaleDegreeFunction.Dominant,
        6 => ScaleDegreeFunction.Submediant,
        7 => ScaleDegreeFunction.LeadingTone, // Same as major scale
        _ => throw new ArgumentOutOfRangeException(nameof(_value))
    };
}