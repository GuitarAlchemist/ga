namespace GA.Business.Core.Fretboard.Primitives;

using GA.Core;
using GA.Core.Collections;

/// <inheritdoc cref="IEquatable{RelativeFret}" />
/// <inheritdoc cref="IComparable{RelativeFret}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An instrument RelativeFret (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
/// <remarks>
/// Assuming maximum hand extent is 5 frets.
/// </remarks>
[PublicAPI]
public readonly record struct RelativeFret : IStaticValueObjectList<RelativeFret>
{
    #region IStaticValueObjectList<RelativeFret> Members

    public static IReadOnlyCollection<RelativeFret> Items => ValueObjectUtils<RelativeFret>.Items;
    public static IReadOnlyList<int> Values => ValueObjectUtils<RelativeFret>.Values;

    #endregion

    #region IValueObject<RelativeFret>

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    #endregion

    #region Relational members

    public int CompareTo(RelativeFret other) => _value.CompareTo(other._value);
    public static bool operator <(RelativeFret left, RelativeFret right) => left.CompareTo(right) < 0;
    public static bool operator >(RelativeFret left, RelativeFret right) => left.CompareTo(right) > 0;
    public static bool operator <=(RelativeFret left, RelativeFret right) => left.CompareTo(right) <= 0;
    public static bool operator >=(RelativeFret left, RelativeFret right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 0;
    private const int _maxValue = 4;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RelativeFret FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static RelativeFret Min => _lazyDefaults.Value.DefaultMin;
    public static RelativeFret Max => _lazyDefaults.Value.DefaultMax;
    public static RelativeFret Zero => _lazyDefaults.Value.DefaultZero;
    public static RelativeFret One => _lazyDefaults.Value.DefaultOne;
    public static RelativeFret Two => _lazyDefaults.Value.DefaultTwo;
    public static RelativeFret Three => _lazyDefaults.Value.DefaultThree;
    public static RelativeFret Four => _lazyDefaults.Value.DefaultFour;

    public static int CheckRange(int value) => IValueObject<RelativeFret>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IValueObject<RelativeFret>.EnsureValueInRange(value, minValue, maxValue);
    public static IReadOnlyCollection<RelativeFret> Range(int start, int count) => ValueObjectUtils<RelativeFret>.GetItems(start, count);

    public static implicit operator RelativeFret(int value) => new() { Value = value };
    public static implicit operator int(RelativeFret relativeFret) => relativeFret.Value;

    private static readonly Lazy<Defaults> _lazyDefaults = new(() => new());

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<RelativeFret>.CheckRange(Value, _minValue, maxValue);

    public override string ToString() => _value.ToString();

    private class Defaults
    {
        public RelativeFret DefaultMin { get; }= FromValue(_minValue);
        public RelativeFret DefaultMax { get; } =FromValue(_maxValue);
        public RelativeFret DefaultZero { get; } = FromValue(1);
        public RelativeFret DefaultOne { get; } = FromValue(1);
        public RelativeFret DefaultTwo { get; } = FromValue(2);
        public RelativeFret DefaultThree { get; } = FromValue(3);
        public RelativeFret DefaultFour { get; } = FromValue(4);
    }
}