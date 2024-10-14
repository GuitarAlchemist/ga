namespace GA.Business.Core.Tonal.Primitives;

/// <remarks>
/// Implements <see cref="IStaticReadonlyCollection{HarmonicMinorScaleDegree}"/>, <see cref="IRangeValueObject{MajorPentatonicScaleDegree}"/>
/// </remarks>
[PublicAPI]
public readonly record struct MajorPentatonicScaleDegree : IRangeValueObject<MajorPentatonicScaleDegree>
{
    #region Relational members

    public int CompareTo(MajorPentatonicScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(MajorPentatonicScaleDegree left, MajorPentatonicScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(MajorPentatonicScaleDegree left, MajorPentatonicScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(MajorPentatonicScaleDegree left, MajorPentatonicScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(MajorPentatonicScaleDegree left, MajorPentatonicScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 5;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MajorPentatonicScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static MajorPentatonicScaleDegree Min => FromValue(_minValue);
    public static MajorPentatonicScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => ValueObjectUtils<MajorPentatonicScaleDegree>.EnsureValueRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<MajorPentatonicScaleDegree>.EnsureValueRange(value, minValue, maxValue);

    public static implicit operator MajorPentatonicScaleDegree(int value) => FromValue(value);
    public static implicit operator int(MajorPentatonicScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<MajorPentatonicScaleDegree> All => ValueObjectUtils<MajorPentatonicScaleDegree>.Items;
    public static IReadOnlyCollection<MajorPentatonicScaleDegree> Items => ValueObjectUtils<MajorPentatonicScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public override string ToString() => Value.ToString();

    public ScaleDegreeFunction ToFunction()
    {
        return _value switch
        {
            1 => ScaleDegreeFunction.Tonic,
            2 => ScaleDegreeFunction.Supertonic,
            3 => ScaleDegreeFunction.Mediant,
            4 => ScaleDegreeFunction.Dominant,
            5 => ScaleDegreeFunction.Submediant,
            _ => throw new ArgumentOutOfRangeException(nameof(_value))
        };
    }
}