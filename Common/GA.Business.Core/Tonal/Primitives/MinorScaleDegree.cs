using System.Runtime.CompilerServices;

namespace GA.Business.Core.Tonal.Primitives;

/// <inheritdoc cref="IEquatable{MinorScaleDegree}" />
/// <inheritdoc cref="IComparable{MinorScaleDegree}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An music minor scale degree - See https://en.wikipedia.org/wiki/Degree_(music)
/// </summary>
[PublicAPI]
public readonly record struct MinorScaleDegree : IValue<MinorScaleDegree>
{
    #region Relational members

    public int CompareTo(MinorScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(MinorScaleDegree left, MinorScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(MinorScaleDegree left, MinorScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(MinorScaleDegree left, MinorScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(MinorScaleDegree left, MinorScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static MinorScaleDegree Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static MinorScaleDegree Min => Create(_minValue);
    public static MinorScaleDegree Max => Create(_maxValue);

    public static int CheckRange(int value) => ValueUtils<MinorScaleDegree>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueUtils<MinorScaleDegree>.CheckRange(value, minValue, maxValue);

    public static implicit operator MinorScaleDegree(int value) => Create(value);
    public static implicit operator int(MinorScaleDegree degree) => degree.Value;

    /// <summary> 1st degree </summary>
    public static MinorScaleDegree Tonic => Create(1);
    /// <summary> 2nd degree </summary>
    public static MinorScaleDegree Supertonic => Create(2);
    /// <summary> 3rd degree </summary>
    public static MinorScaleDegree Mediant => Create(3);
    /// <summary> 4th degree </summary>
    public static MinorScaleDegree Subdominant => Create(4);
    /// <summary> 5th degree </summary>
    public static MinorScaleDegree Dominant => Create(5);
    /// <summary> 6th degree </summary>
    public static MinorScaleDegree Submediant => Create(6);
    /// <summary> 7th degree (b7) </summary>
    public static MinorScaleDegree Subtonic => Create(7);

    public static IReadOnlyCollection<MinorScaleDegree> All => ValueUtils<MinorScaleDegree>.GetAll();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public override string ToString() => Value.ToString();

    public string ToFunction()
    {
        return _value switch
        {
            1 => "Tonic",
            2 => "Supertonic",
            3 => "Mediant",
            4 => "Sub-dominant",
            5 => "Dominant",
            6 => "Sub-mediant",
            7 => "Sub-tonic",
            _ => throw new ArgumentOutOfRangeException(nameof(_value))
        };
    }
}