namespace GA.Business.Core.Fretboard.Primitives;

using GA.Core;
using GA.Core.Collections;

/// <summary>
/// An instrument string (Between <see cref="Min"/> and <see cref="Max" />)
/// </summary>
/// <remarks>
/// String 1 is the string with the highest pitch.
/// </remarks>
[PublicAPI]
public readonly record struct Str : IValueObject<Str>
{
    #region Relational members

    public int CompareTo(Str other) => _value.CompareTo(other._value);
    public static bool operator <(Str left, Str right) => left.CompareTo(right) < 0;
    public static bool operator >(Str left, Str right) => left.CompareTo(right) > 0;
    public static bool operator <=(Str left, Str right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Str left, Str right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 26;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Str FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };
    public static Str operator ++(Str str) => FromValue(str._value + 1);

    /// <summary>
    /// The first string (Highest pitch)
    /// </summary>
    public static Str Min => FromValue(_minValue);
    /// <summary>
    /// The last string (Lowest pitch)
    /// </summary>
    public static Str Max => FromValue(_maxValue);
    public static int CheckRange(int value) => ValueObjectUtils<Str>.CheckRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => ValueObjectUtils<Str>.CheckRange(value, minValue, maxValue);
    public static IReadOnlyCollection<Str> Range(int count) => ValueObjectUtils<Str>.GetItems(_minValue, count);

    public static implicit operator Str(int value) => new() { Value = value };
    public static implicit operator int(Str str) => str._value;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<Str>.CheckRange(Value, _minValue, maxValue);
    public override string ToString() => Value.ToString();
}