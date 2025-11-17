namespace GA.Business.Core.Fretboard.Primitives;

using System;
using static ValueObjectUtils<Fret>;

/// <summary>
///     An instrument fret (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
/// <remarks>
///     Implements <see cref="IStaticValueObjectList{Fret}" />
/// </remarks>
[PublicAPI]
public readonly record struct Fret : IStaticValueObjectList<Fret>
{
    private const int _minValue = -1;
    private const int _maxValue = 36;

    /// <summary>
    ///     Creates a new Fret from an int value with range validation.
    /// </summary>
    /// <param name="value">
    ///     The fret number. Must be between <see cref="Min" /> (-1) and <see cref="Max" /> (36). Use -1 for
    ///     <see cref="Muted" />, 0 for <see cref="Open" />, or 1-36 for fretted positions.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="value" /> is outside the valid range
    ///     [-1..36].
    /// </exception>
    /// <remarks>
    ///     You can also use implicit conversion: <c>Fret fret = 5;</c> or static properties like <see cref="Open" />,
    ///     <see cref="Muted" />.
    /// </remarks>
    public Fret([ValueRange(_minValue, _maxValue)] int value)
    {
        _value = CheckRange(value);
    }

    public static Fret Min => FromValue(_minValue);
    public static Fret Max => FromValue(_maxValue);
    public static readonly Fret Muted = new(-1);
    public static readonly Fret Open  = new(0);
    public static readonly Fret One   = new(1);
    public static readonly Fret Two   = new(2);
    public static readonly Fret Three = new(3);
    public static readonly Fret Four  = new(4);
    public static readonly Fret Five  = new(5);

    public bool IsMuted => this == Muted;
    public bool IsOpen => this == Open;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fret FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };
    public static implicit operator Fret(int value) => new() { Value = value };
    public static implicit operator int(Fret fret) => fret.Value;

    /// <summary>
    /// Returns the <see cref="FretKind"/> of the fret.
    /// </summary>
    public FretKind Kind => _value switch
    {
        -1 => FretKind.Muted,
        0 => FretKind.Open,
        _  => FretKind.Fretted
    };

    /// <summary>
    /// Returns the fret number if the fret is fretted, otherwise null.
    /// </summary>
    public int? FrettedNumber =>
        Kind == FretKind.Fretted
            ? _value
            : null;

    /// <summary>
    ///     Attempts to create a Fret from an int value, returning a Result instead of throwing.
    /// </summary>
    /// <param name="value">The fret number to validate.</param>
    /// <returns>A Result containing either a valid Fret or an error message.</returns>
    /// <remarks>
    ///     This method enables functional error handling without exceptions.
    ///     Example:
    ///     <code>
    /// var result = Fret.TryCreate(userInput)
    ///     .Map(fret => fret.Value)
    ///     .Match(
    ///         onSuccess: v => $"Valid fret: {v}",
    ///         onFailure: err => $"Error: {err}"
    ///     );
    /// </code>
    /// </remarks>
    public static Result<Fret, string> TryCreate(int value) => value is < _minValue or > _maxValue
        ? Result<Fret, string>.Failure($"Fret number must be between {_minValue} (muted) and {_maxValue}, got {value}")
        : Result<Fret, string>.Success(new Fret { Value = value });

    public static int CheckRange(int value) =>
        IRangeValueObject<Fret>.EnsureValueInRange(value, _minValue, _maxValue);

    public static int CheckRange(int value, int minValue, int maxValue) =>
        IRangeValueObject<Fret>.EnsureValueInRange(value, minValue, maxValue);

    public static IReadOnlyCollection<Fret> Range(int start, int count) =>
        GetItems(start, count);

    public static IReadOnlyCollection<Fret> Range(int start, int count, bool includeOpen) =>
        includeOpen ? GetItemsWithHead(Open, start, count) : Range(start, count);

    public static ImmutableSortedSet<Fret> Set(Range range) =>
        Set(Enumerable.Range(range.Start.Value, range.End.Value - range.Start.Value));

    public static ImmutableSortedSet<Fret> Set(IEnumerable<int> values) =>
        [..values.Select(FromValue)];

    public static ImmutableSortedSet<Fret> Set(params int[] values) =>
        [..values];

    public static ImmutableSortedSet<Fret> Set(int value, Range range) =>
        Set(value, Enumerable.Range(range.Start.Value, range.End.Value - range.Start.Value + 1));

    public static ImmutableSortedSet<Fret> Set(int value, params int[] values) =>
        Set(value, values.AsEnumerable());

    public static ImmutableSortedSet<Fret> Set(int value, IEnumerable<int> values) =>
        Set(new[] { value }.Union(values));

    public static Fret operator +(Fret fret, RelativeFret relativeFret) =>
        new() { Value = fret.Value + relativeFret.Value };

    public void CheckMaxValue(int maxValue) =>
        EnsureValueRange(Value, _minValue, maxValue);

    public override string ToString() => _value switch
    {
        -1 => "x",
        0 => "O",
        _ => Value.ToString()
    };

    #region IStaticValueObjectList<Fret> Members

    /// <summary>
    /// Gets all Fret instances (automatically memoized).
    /// </summary>
    public static IReadOnlyCollection<Fret> Items => ValueObjectUtils<Fret>.Items;

    /// <summary>
    /// Gets all Fret values (automatically memoized).
    /// </summary>
    public static IReadOnlyList<int> Values => ValueObjectUtils<Fret>.Values;

    /// <summary>
    /// Gets the cached span representing the full fret range.
    /// </summary>
    public static ReadOnlySpan<Fret> ItemsSpan => ValueObjectUtils<Fret>.ItemsSpan;

    /// <summary>
    /// Gets the cached span representing the numeric values for each fret.
    /// </summary>
    public static ReadOnlySpan<int> ValuesSpan => ValueObjectUtils<Fret>.ValuesSpan;

    #endregion

    #region IValueObject<Fret>

    private readonly int _value;

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    #endregion

    #region Relational members

    public int CompareTo(Fret other) =>
        _value.CompareTo(other._value);

    public static bool operator <(Fret left, Fret right) =>
         left.CompareTo(right) < 0;

    public static bool operator >(Fret left, Fret right) =>
         left.CompareTo(right) > 0;

    public static bool operator <=(Fret left, Fret right) =>
        left.CompareTo(right) <= 0;

    public static bool operator >=(Fret left, Fret right) =>
        left.CompareTo(right) >= 0;

    #endregion
}
