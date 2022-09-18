namespace GA.Business.Core.Tonal.Primitives;

using Modes;

/// <summary>
/// An Objects minor scale degree - See https://en.wikipedia.org/wiki/Degree_(Objects)
/// </summary>
[PublicAPI]
public readonly record struct NaturalMinorScaleDegree : IMinorScaleModeDegree<NaturalMinorScaleDegree>,
                                                        IMusicObjectCollection<NaturalMinorScaleDegree>
{
    #region Relational members

    public int CompareTo(NaturalMinorScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(NaturalMinorScaleDegree left, NaturalMinorScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NaturalMinorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static NaturalMinorScaleDegree Min => FromValue(_minValue);
    public static NaturalMinorScaleDegree Max => FromValue(_maxValue);
    public static NaturalMinorScaleDegree Aeolian => new() { Value = DegreeValue.AeolianValue };
    public static NaturalMinorScaleDegree Locrian => new() { Value = DegreeValue.LocrianValue };
    public static NaturalMinorScaleDegree Ionian => new() { Value = DegreeValue.IonianValue };
    public static NaturalMinorScaleDegree Dorian => new() { Value = DegreeValue.DorianValue };
    public static NaturalMinorScaleDegree Phrygian => new() { Value = DegreeValue.PhrygianValue };
    public static NaturalMinorScaleDegree Lydian => new() { Value = DegreeValue.LydianValue };
    public static NaturalMinorScaleDegree Mixolydian => new() { Value = DegreeValue.MixolydianValue };

    public static int CheckRange(int value) => ValueObjectUtils<NaturalMinorScaleDegree>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<NaturalMinorScaleDegree>.CheckRange(value, minValue, maxValue);

    public static implicit operator NaturalMinorScaleDegree(int value) => FromValue(value);
    public static implicit operator int(NaturalMinorScaleDegree degree) => degree.Value;

    public static IEnumerable<NaturalMinorScaleDegree> Objects => ValueObjectUtils<NaturalMinorScaleDegree>.Items;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public override string ToString() => Value.ToString();

    public ScaleDegreeFunction ToFunction() => _value switch
    {
        DegreeValue.AeolianValue => ScaleDegreeFunction.Tonic,
        DegreeValue.LocrianValue => ScaleDegreeFunction.Supertonic,
        DegreeValue.IonianValue => ScaleDegreeFunction.Mediant,
        DegreeValue.DorianValue => ScaleDegreeFunction.Subdominant,
        DegreeValue.PhrygianValue => ScaleDegreeFunction.Dominant,
        DegreeValue.LydianValue => ScaleDegreeFunction.Submediant,
        DegreeValue.MixolydianValue => ScaleDegreeFunction.Subtonic,
        _ => throw new ArgumentOutOfRangeException(nameof(_value))
    };

    private static class DegreeValue
    {
        public const int AeolianValue = 1;
        public const int LocrianValue = 2;
        public const int IonianValue = 3;
        public const int DorianValue = 4;
        public const int PhrygianValue = 5;
        public const int LydianValue = 6;
        public const int MixolydianValue = 7;
    }
}