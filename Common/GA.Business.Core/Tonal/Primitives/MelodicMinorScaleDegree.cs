namespace GA.Business.Core.Tonal.Primitives;

using GA.Core.Collections;
using GA.Core.Extensions;




using Modes;

/// <inheritdoc cref="IEquatable{MelodicMinorScaleDegree}" />
/// <inheritdoc cref="IComparable{MelodicMinorScaleDegree}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An Objects minor scale degree - See https://en.wikipedia.org/wiki/Degree_(Objects)
/// </summary>
[PublicAPI]
public readonly record struct MelodicMinorScaleDegree : IMinorScaleModeDegree<MelodicMinorScaleDegree>, 
                                                        IMusicObjectCollection<MelodicMinorScaleDegree>
{
    #region Relational members

    public int CompareTo(MelodicMinorScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(MelodicMinorScaleDegree left, MelodicMinorScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(MelodicMinorScaleDegree left, MelodicMinorScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(MelodicMinorScaleDegree left, MelodicMinorScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(MelodicMinorScaleDegree left, MelodicMinorScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MelodicMinorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static MelodicMinorScaleDegree Min => FromValue(_minValue);
    public static MelodicMinorScaleDegree Max => FromValue(_maxValue);
    public static MelodicMinorScaleDegree MelodicMinorModeMinor => new() { Value = DegreeValue.MelodicMinorModeMinorValue };
    public static MelodicMinorScaleDegree DorianFlatSecond => new() { Value = DegreeValue.DorianFlatSecondValue };
    public static MelodicMinorScaleDegree LydianAugmented => new() { Value = DegreeValue.LydianAugmentedValue };
    public static MelodicMinorScaleDegree LydianDominant => new() { Value = DegreeValue.LydianDominantValue };
    public static MelodicMinorScaleDegree MixolydianFlatSixth => new() { Value = DegreeValue.MixolydianFlatSixthValue };
    public static MelodicMinorScaleDegree LocrianNaturalSecond => new() { Value = DegreeValue.LocrianNaturalSecondValue };
    public static MelodicMinorScaleDegree Altered => new() { Value = DegreeValue.AlteredValue };

    public static int CheckRange(int value) => ValueObjectUtils<MelodicMinorScaleDegree>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<MelodicMinorScaleDegree>.CheckRange(value, minValue, maxValue);

    public static implicit operator MelodicMinorScaleDegree(int value) => FromValue(value);
    public static implicit operator int(MelodicMinorScaleDegree degree) => degree.Value;

    public static IEnumerable<MelodicMinorScaleDegree> Objects => ValueObjectUtils<MelodicMinorScaleDegree>.Items;
    public static IReadOnlyCollection<MelodicMinorScaleDegree> Items => ValueObjectUtils<MelodicMinorScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.ToValues();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public override string ToString() => Value.ToString();

    public ScaleDegreeFunction ToFunction() => _value switch
    {
        DegreeValue.MelodicMinorModeMinorValue => ScaleDegreeFunction.Tonic,
        DegreeValue.DorianFlatSecondValue => ScaleDegreeFunction.Supertonic,
        DegreeValue.LydianAugmentedValue => ScaleDegreeFunction.Mediant,
        DegreeValue.LydianDominantValue => ScaleDegreeFunction.Subdominant,
        DegreeValue.MixolydianFlatSixthValue => ScaleDegreeFunction.Dominant,
        DegreeValue.LocrianNaturalSecondValue => ScaleDegreeFunction.Submediant,
        DegreeValue.AlteredValue => ScaleDegreeFunction.LeadingTone, // Same as major scale
        _ => throw new ArgumentOutOfRangeException(nameof(_value))
    };

    private static class DegreeValue
    {
        public const int MelodicMinorModeMinorValue = 1;
        public const int DorianFlatSecondValue = 2;
        public const int LydianAugmentedValue = 3;
        public const int LydianDominantValue = 4;
        public const int MixolydianFlatSixthValue = 5;
        public const int LocrianNaturalSecondValue = 6;
        public const int AlteredValue = 7;
    }
}