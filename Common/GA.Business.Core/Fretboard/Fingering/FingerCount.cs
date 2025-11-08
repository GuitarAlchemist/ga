namespace GA.Business.Core.Fretboard.Fingering;

/// <inheritdoc cref="IEquatable{FingerCount}" />
/// <inheritdoc cref="IComparable{FingerCount}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
///     Finger count needed for a position on left hand or right hand for lefties (Between <see cref="Min" /> and
///     <see cref="Max" />)
/// </summary>
[PublicAPI]
public readonly record struct FingerCount : IStaticValueObjectList<FingerCount>
{
    private const int _minValue = 0;
    private const int _maxValue = 5;

    private static readonly Lazy<Defaults> _lazyDefaults = new(() => new());
    public static FingerCount Zero => _lazyDefaults.Value.DefaultZero;
    public static FingerCount One => _lazyDefaults.Value.DefaultOne;
    public static FingerCount Two => _lazyDefaults.Value.DefaultTwo;
    public static FingerCount Three => _lazyDefaults.Value.DefaultThree;
    public static FingerCount Four => _lazyDefaults.Value.DefaultFour;
    public static FingerCount Five => _lazyDefaults.Value.DefaultFive;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FingerCount FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new FingerCount { Value = value };
    }

    public static FingerCount Min => _lazyDefaults.Value.DefaultMin;
    public static FingerCount Max => _lazyDefaults.Value.DefaultMax;

    public static implicit operator FingerCount(int value)
    {
        return new FingerCount { Value = value };
    }

    public static implicit operator int(FingerCount fingerCount)
    {
        return fingerCount.Value;
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<FingerCount>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<FingerCount>.EnsureValueInRange(value, minValue, maxValue);
    }

    public void CheckMaxValue(int maxValue)
    {
        ValueObjectUtils<FingerCount>.EnsureValueRange(Value, _minValue, maxValue);
    }

    public override string ToString()
    {
        return _value switch { _ => Value.ToString() };
    }

    private class Defaults
    {
        public FingerCount DefaultMin { get; } = FromValue(_minValue);
        public FingerCount DefaultMax { get; } = FromValue(_maxValue);
        public FingerCount DefaultZero { get; } = FromValue(0);
        public FingerCount DefaultOne { get; } = FromValue(1);
        public FingerCount DefaultTwo { get; } = FromValue(2);
        public FingerCount DefaultThree { get; } = FromValue(3);
        public FingerCount DefaultFour { get; } = FromValue(4);
        public FingerCount DefaultFive { get; } = FromValue(5);
    }

    #region IStaticValueObjectList<FingerCount> Members

    public static IReadOnlyCollection<FingerCount> Items => ValueObjectUtils<FingerCount>.Items;
    public static IReadOnlyList<int> Values => ValueObjectUtils<FingerCount>.Values;

    #endregion

    #region IValueObject<FingerCount>

    private readonly int _value;

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    #endregion

    #region Relational members

    public int CompareTo(FingerCount other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(FingerCount left, FingerCount right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(FingerCount left, FingerCount right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(FingerCount left, FingerCount right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(FingerCount left, FingerCount right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
