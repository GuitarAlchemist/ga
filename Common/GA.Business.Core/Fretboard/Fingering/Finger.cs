namespace GA.Business.Core.Fretboard.Fingering;

using GA.Core;
using GA.Core.Collections;

/// <inheritdoc cref="IEquatable{Finger}" />
/// <inheritdoc cref="IComparable{Finger}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An instrument Finger (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
[PublicAPI]
public readonly record struct Finger : IStaticValueObjectList<Finger>
{
    #region IStaticValueObjectList<Finger> Members

    public static IReadOnlyCollection<Finger> Items => ValueObjectUtils<Finger>.Items;
    public static IReadOnlyList<int> Values => ValueObjectUtils<Finger>.Values;

    #endregion

    #region IValueObject<Finger>

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    #endregion

    #region Relational members

    public int CompareTo(Finger other) => _value.CompareTo(other._value);
    public static bool operator <(Finger left, Finger right) => left.CompareTo(right) < 0;
    public static bool operator >(Finger left, Finger right) => left.CompareTo(right) > 0;
    public static bool operator <=(Finger left, Finger right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Finger left, Finger right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 0;
    private const int _maxValue = 4;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Finger FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Finger Min => _lazyDefaults.Value.DefaultMin;
    public static Finger Max => _lazyDefaults.Value.DefaultMax;
    public static Finger Thumb => _lazyDefaults.Value.DefaultThumb;
    public static Finger Index => _lazyDefaults.Value.DefaultIndex;
    public static Finger Middle => _lazyDefaults.Value.DefaultMiddle;
    public static Finger Ring => _lazyDefaults.Value.DefaultRing;
    public static Finger Pinky => _lazyDefaults.Value.DefaultPinky;

    public static int CheckRange(int value) => IValueObject<Finger>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IValueObject<Finger>.EnsureValueInRange(value, minValue, maxValue);
    public static IReadOnlyCollection<Finger> Range(int start, int count) => ValueObjectUtils<Finger>.GetItems(start, count);

    public static implicit operator Finger(int value) => new() { Value = value };
    public static implicit operator int(Finger finger) => finger.Value;

    private static readonly Lazy<Defaults> _lazyDefaults = new(() => new());

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<Finger>.CheckRange(Value, _minValue, maxValue);

    public override string ToString() => _value switch
    {
        0 => "T",
        _ => _value.ToString()
    };

    private class Defaults
    {
        public Finger DefaultMin { get; }= FromValue(_minValue);
        public Finger DefaultMax { get; } =FromValue(_maxValue);
        public Finger DefaultThumb { get; } = FromValue(0);
        public Finger DefaultIndex { get; } = FromValue(1);
        public Finger DefaultMiddle { get; } = FromValue(2);
        public Finger DefaultRing { get; } = FromValue(3);
        public Finger DefaultPinky { get; } = FromValue(4);
    }
}