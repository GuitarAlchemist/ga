namespace GA.Business.Core.Tonal.Primitives;

using System.Runtime.CompilerServices;

/// <inheritdoc cref="IEquatable{MajorScaleDegree}" />
/// <inheritdoc cref="IComparable{MajorScaleDegree}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An music degree major scale - See https://en.wikipedia.org/wiki/Degree_(music)
/// </summary>
[PublicAPI]
public readonly record struct MajorScaleDegree : IValue<MajorScaleDegree>, IAll<MajorScaleDegree>
{
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
    private static MajorScaleDegree Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static MajorScaleDegree Min => Create(_minValue);
    public static MajorScaleDegree Max => Create(_maxValue);

    public static int CheckRange(int value) => ValueUtils<MajorScaleDegree>.CheckRange(value, _minValue, _maxValue);

    public static implicit operator MajorScaleDegree(int value) => Create(value);
    public static implicit operator int(MajorScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<MajorScaleDegree> All => ValueUtils<MajorScaleDegree>.GetAll();

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
            7 => ScaleDegreeFunction.LeadingTone,
            _ => throw new ArgumentOutOfRangeException(nameof(_value))
        };
    }
}