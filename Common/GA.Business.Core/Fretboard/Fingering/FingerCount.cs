namespace GA.Business.Core.Fretboard.Fingering;

using GA.Core;
using GA.Core.Collections;

/// <inheritdoc cref="IEquatable{FingerCount}" />
/// <inheritdoc cref="IComparable{FingerCount}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// Finger count needed for a position on left hand or right hand for lefties (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
[PublicAPI]
public readonly record struct FingerCount : IStaticValueObjectList<FingerCount>
{
    #region IStaticValueObjectList<FingerCount> Members

    public static IReadOnlyCollection<FingerCount> Items => ValueObjectUtils<FingerCount>.Items;
    public static IReadOnlyList<int> Values => ValueObjectUtils<FingerCount>.Values;

    #endregion

    #region IValueObject<FingerCount>

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    #endregion

    #region Relational members

    public int CompareTo(FingerCount other) => _value.CompareTo(other._value);
    public static bool operator <(FingerCount left, FingerCount right) => left.CompareTo(right) < 0;
    public static bool operator >(FingerCount left, FingerCount right) => left.CompareTo(right) > 0;
    public static bool operator <=(FingerCount left, FingerCount right) => left.CompareTo(right) <= 0;
    public static bool operator >=(FingerCount left, FingerCount right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = -1;
    private const int _maxValue = 36;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FingerCount FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static FingerCount Min => _lazyDefaults.Value.DefaultMin;
    public static FingerCount Max => _lazyDefaults.Value.DefaultMax;
    public static FingerCount Zero => _lazyDefaults.Value.DefaultZero;
    public static FingerCount One => _lazyDefaults.Value.DefaultOne;
    public static FingerCount Two => _lazyDefaults.Value.DefaultTwo;
    public static FingerCount Three => _lazyDefaults.Value.DefaultThree;
    public static FingerCount Four => _lazyDefaults.Value.DefaultFour;
    public static FingerCount Five => _lazyDefaults.Value.DefaultFive;

    public static int CheckRange(int value) => IValueObject<FingerCount>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IValueObject<FingerCount>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator FingerCount(int value) => new() { Value = value };
    public static implicit operator int(FingerCount fingerCount) => fingerCount.Value;

    private static readonly Lazy<Defaults> _lazyDefaults = new(() => new());

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<FingerCount>.CheckRange(Value, _minValue, maxValue);

    public override string ToString() => _value switch {_ => Value.ToString() };

    private class Defaults
    {
        public Defaults()
        {
            DefaultMin = FromValue(_minValue);
            DefaultMax = FromValue(_maxValue);
            DefaultZero = FromValue(0);
            DefaultOne = FromValue(1);
            DefaultTwo = FromValue(2);
            DefaultThree = FromValue(3);
            DefaultFour = FromValue(4);
            DefaultFive = FromValue(5);
        }

        public FingerCount DefaultMin { get; }
        public FingerCount DefaultMax { get; }
        public FingerCount DefaultZero { get; }
        public FingerCount DefaultOne { get; }
        public FingerCount DefaultTwo { get; }
        public FingerCount DefaultThree { get; }
        public FingerCount DefaultFour { get; }
        public FingerCount DefaultFive { get; }
    }
}