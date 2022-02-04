namespace GA.Business.Core.Tonal.Primitives;

using System.Runtime.CompilerServices;

/// <inheritdoc cref="IEquatable{MajorScaleDegree}" />
/// <inheritdoc cref="IComparable{MajorScaleDegree}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An music scale degree - See https://en.wikipedia.org/wiki/Degree_(music)
/// </summary>
[PublicAPI]
public readonly record struct ModalScaleDegree : IValue<ModalScaleDegree>
{
    #region Relational members

    public int CompareTo(ModalScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(ModalScaleDegree left, ModalScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(ModalScaleDegree left, ModalScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(ModalScaleDegree left, ModalScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(ModalScaleDegree left, ModalScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 7;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ModalScaleDegree Create([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static ModalScaleDegree Min => Create(_minValue);
    public static ModalScaleDegree Max => Create(_maxValue);
    public static int CheckRange(int value) => ValueUtils<ModalScaleDegree>.CheckRange(value, _minValue, _maxValue);

    public static implicit operator ModalScaleDegree(int value) => Create(value);
    public static implicit operator int(ModalScaleDegree degree) => degree.Value;

    /// <summary> 1st degree </summary>
    public static ModalScaleDegree Tonic => Create(1);
    /// <summary> 2nd degree </summary>
    public static ModalScaleDegree Supertonic => Create(2);
    /// <summary> 3nd degree </summary>
    public static ModalScaleDegree Mediant => Create(3);
    /// <summary> 4th degree </summary>
    public static ModalScaleDegree Subdominant => Create(4);
    /// <summary> 5th degree </summary>
    public static ModalScaleDegree Dominant => Create(5);
    /// <summary> 6th degree </summary>
    public static ModalScaleDegree Submediant => Create(6);
    /// <summary> 7th degree </summary>
    public static ModalScaleDegree LeadingTone => Create(7);

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public override string ToString() => Value.ToString();
}