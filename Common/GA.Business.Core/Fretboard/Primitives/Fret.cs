namespace GA.Business.Core.Fretboard.Primitives;

using GA.Core;
using GA.Core.Collections;

/// <inheritdoc cref="IEquatable{Fret}" />
/// <inheritdoc cref="IComparable{Fret}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
/// An instrument fret (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
[PublicAPI]
public readonly record struct Fret : IStaticValueObjectList<Fret>
{
    #region IStaticValueObjectList<Fret> Members

    public static IReadOnlyCollection<Fret> Items => ValueObjectUtils<Fret>.Items;
    public static IReadOnlyList<int> Values => ValueObjectUtils<Fret>.Values;

    #endregion

    #region IValueObject<Fret>

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    #endregion

    #region Relational members

    public int CompareTo(Fret other) => _value.CompareTo(other._value);
    public static bool operator <(Fret left, Fret right) => left.CompareTo(right) < 0;
    public static bool operator >(Fret left, Fret right) => left.CompareTo(right) > 0;
    public static bool operator <=(Fret left, Fret right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Fret left, Fret right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = -1;
    private const int _maxValue = 36;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fret FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    public static Fret Min => _lazyDefaults.Value.DefaultMin;
    public static Fret Max => _lazyDefaults.Value.DefaultMax;
    public static Fret Muted => _lazyDefaults.Value.DefaultMuted;
    public static Fret Open => _lazyDefaults.Value.DefaultOpen;
    public static Fret One => _lazyDefaults.Value.DefaultOne;
    public static Fret Two => _lazyDefaults.Value.DefaultTwo;
    public static Fret Three => _lazyDefaults.Value.DefaultThree;
    public static Fret Four => _lazyDefaults.Value.DefaultFour;
    public static Fret Five => _lazyDefaults.Value.DefaultFive;

    public static int CheckRange(int value) => IValueObject<Fret>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IValueObject<Fret>.EnsureValueInRange(value, minValue, maxValue);
    public static IReadOnlyCollection<Fret> Range(int start, int count) => ValueObjectUtils<Fret>.GetItems(start, count);
    public static IReadOnlyCollection<Fret> Range(int start, int count, bool includeOpen) => includeOpen ? ValueObjectUtils<Fret>.GetItemsWithHead(Open, start, count) : Range(start, count);
    public static ImmutableSortedSet<Fret> Set(Range range) => Set(Enumerable.Range(range.Start.Value, range.End.Value - range.Start.Value));
    public static ImmutableSortedSet<Fret> Set(IEnumerable<int> values) => values.Select(FromValue).ToImmutableSortedSet();
    public static ImmutableSortedSet<Fret> Set(params int[] values) => Set(values.AsEnumerable());
    public static ImmutableSortedSet<Fret> Set(int value1, Range range) => Set(value1, Enumerable.Range(range.Start.Value, range.End.Value - range.Start.Value + 1));
    public static ImmutableSortedSet<Fret> Set(int value1, params int[] values) => Set(value1, values.AsEnumerable());
    public static ImmutableSortedSet<Fret> Set(int value1, IEnumerable<int> values) => Set(new [] {value1}.Union(values));

    public static implicit operator Fret(int value) => new() { Value = value };
    public static implicit operator int(Fret fret) => fret.Value;

    public bool IsMuted => this == Muted;
    public bool IsOpen => this == Open;

    private static readonly Lazy<Defaults> _lazyDefaults = new(() => new());

    public void CheckMaxValue(int maxValue) => ValueObjectUtils<Fret>.CheckRange(Value, _minValue, maxValue);

    public override string ToString() => _value switch
    {
        -1 => "x",
        0 => "O",
        _ => Value.ToString()
    };

    private class Defaults
    {
        public Fret DefaultMin { get; }= FromValue(_minValue);
        public Fret DefaultMax { get; } =FromValue(_maxValue);
        public Fret DefaultMuted { get; }= FromValue(-1);
        public Fret DefaultOpen { get; } = FromValue(0);
        public Fret DefaultOne { get; } = FromValue(1);
        public Fret DefaultTwo { get; } = FromValue(2);
        public Fret DefaultThree { get; } = FromValue(3);
        public Fret DefaultFour { get; } = FromValue(4);
        public Fret DefaultFive { get; } = FromValue(5);
    }
}