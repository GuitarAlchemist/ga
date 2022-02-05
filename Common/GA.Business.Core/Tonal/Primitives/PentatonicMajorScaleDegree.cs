namespace GA.Business.Core.Tonal.Primitives;

using System.Runtime.CompilerServices;

/// <inheritdoc cref="IEquatable{MelodicMinorScaleDegree}" />
/// <inheritdoc cref="IComparable{MelodicMinorScaleDegree}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An music minor scale degree - See https://en.wikipedia.org/wiki/Degree_(music)
/// </summary>
[PublicAPI]
public readonly record struct MelodicMinorScaleDegree : IValue<MelodicMinorScaleDegree>
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
    private static MelodicMinorScaleDegree Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static MelodicMinorScaleDegree Min => Create(_minValue);
    public static MelodicMinorScaleDegree Max => Create(_maxValue);

    public static int CheckRange(int value) => ValueUtils<MelodicMinorScaleDegree>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<MelodicMinorScaleDegree>.CheckRange(value, minValue, maxValue);

    public static implicit operator MelodicMinorScaleDegree(int value) => Create(value);
    public static implicit operator int(MelodicMinorScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<MelodicMinorScaleDegree> All => ValueUtils<MelodicMinorScaleDegree>.GetAll();

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
            7 => ScaleDegreeFunction.LeadingTone, // Same as major scale
            _ => throw new ArgumentOutOfRangeException(nameof(_value))
        };
    }
}