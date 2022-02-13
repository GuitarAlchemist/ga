namespace GA.Business.Core.Tonal.Primitives;

using System.Runtime.CompilerServices;

/// <inheritdoc cref="IEquatable{NaturalMinorScaleDegree}" />
/// <inheritdoc cref="IComparable{NaturalMinorScaleDegree}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An music minor scale degree - See https://en.wikipedia.org/wiki/Degree_(music)
/// </summary>
[PublicAPI]
public readonly record struct NaturalMinorScaleDegree : IValue<NaturalMinorScaleDegree>
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
    private static NaturalMinorScaleDegree Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static NaturalMinorScaleDegree Min => Create(_minValue);
    public static NaturalMinorScaleDegree Max => Create(_maxValue);
    public static NaturalMinorScaleDegree Aeolian => new() {Value = 1};
    public static NaturalMinorScaleDegree Locrian => new() {Value = 2};
    public static NaturalMinorScaleDegree Ionian => new() {Value = 3};
    public static NaturalMinorScaleDegree Dorian => new() {Value = 4};
    public static NaturalMinorScaleDegree Phrygian => new() {Value = 5};
    public static NaturalMinorScaleDegree Lydian => new() {Value = 6};
    public static NaturalMinorScaleDegree Mixolydian => new() {Value = 7};

    public static int CheckRange(int value) => ValueUtils<NaturalMinorScaleDegree>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<NaturalMinorScaleDegree>.CheckRange(value, minValue, maxValue);

    public static implicit operator NaturalMinorScaleDegree(int value) => Create(value);
    public static implicit operator int(NaturalMinorScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<NaturalMinorScaleDegree> All => ValueUtils<NaturalMinorScaleDegree>.GetAll();

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
            4 => ScaleDegreeFunction.Subdominant,
            5 => ScaleDegreeFunction.Dominant,
            6 => ScaleDegreeFunction.Submediant,
            7 => ScaleDegreeFunction.Subtonic,
            _ => throw new ArgumentOutOfRangeException(nameof(_value))
        };
    }
}