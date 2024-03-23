namespace GA.Business.Core.Tonal.Primitives;

/// <summary>
/// An Objects degree major scale - See https://en.wikipedia.org/wiki/Degree_(Objects)
/// </summary>
[PublicAPI]
public readonly record struct MajorScaleDegree : IStaticValueObjectList<MajorScaleDegree>
{
    #region IStaticValueObjectList<MajorScaleDegree> Members

    public static IReadOnlyCollection<MajorScaleDegree> Items => ValueObjectUtils<MajorScaleDegree>.Items;
    public static IReadOnlyList<int> Values => Items.ToValueList();

    #endregion

    #region IValueObject<MajorScaleDegree>

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    #endregion

    #region Relational members

    public int CompareTo(MajorScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(MajorScaleDegree left, MajorScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(MajorScaleDegree left, MajorScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(MajorScaleDegree left, MajorScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(MajorScaleDegree left, MajorScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MajorScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static MajorScaleDegree Min => FromValue(_minValue);
    public static MajorScaleDegree Max => FromValue(_maxValue);

    public static MajorScaleDegree Ionian => FromValue(DegreeValue.IonianValue);
    public static MajorScaleDegree Dorian =>  FromValue(DegreeValue.DorianValue);
    public static MajorScaleDegree Phrygian => FromValue(DegreeValue.PhrygianValue);
    public static MajorScaleDegree Lydian => FromValue(DegreeValue.LydianValue);
    public static MajorScaleDegree Mixolydian => FromValue(DegreeValue.MixolydianValue);
    public static MajorScaleDegree Aeolian => FromValue(DegreeValue.AeolianValue);
    public static MajorScaleDegree Locrian => FromValue(DegreeValue.LocrianValue);

    public static int CheckRange(int value) => ValueObjectUtils<MajorScaleDegree>.CheckRange(value, _minValue, _maxValue);

    public static implicit operator MajorScaleDegree(int value) => FromValue(value);
    public static implicit operator int(MajorScaleDegree degree) => degree.Value;

    public override string ToString() => Value.ToString();

    public ScaleDegreeFunction ToFunction() => _value switch
    {
        DegreeValue.IonianValue => ScaleDegreeFunction.Tonic,
        DegreeValue.DorianValue=> ScaleDegreeFunction.Supertonic,
        DegreeValue.PhrygianValue => ScaleDegreeFunction.Mediant,
        DegreeValue.LydianValue => ScaleDegreeFunction.Subdominant,
        DegreeValue.MixolydianValue => ScaleDegreeFunction.Dominant,
        DegreeValue.AeolianValue => ScaleDegreeFunction.Submediant,
        DegreeValue.LocrianValue => ScaleDegreeFunction.LeadingTone,
        _ => throw new ArgumentOutOfRangeException(nameof(_value))
    };

    public static class DegreeValue
    {
        public const int IonianValue = 1;
        public const int DorianValue = 2;
        public const int PhrygianValue = 3;
        public const int LydianValue = 4;
        public const int MixolydianValue = 5;
        public const int AeolianValue = 6;
        public const int LocrianValue = 7;
    }
}