namespace GA.Business.Core.Fretboard.Primitives;

/// <summary>
///     An instrument fret (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
/// <remarks>
///     Implements <see cref="IEquatable{Fret}" /> <see cref="IComparable{Fret}" />, <see cref="IComparable" />
/// </remarks>
[PublicAPI]
public readonly record struct Fret : IStaticValueObjectList<Fret>
{
    private const int _minValue = -1;
    private const int _maxValue = 36;

    private static readonly Lazy<Defaults> _lazyDefaults = new(() => new());

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

    public static Fret Muted => _lazyDefaults.Value.DefaultMuted;
    public static Fret Open => _lazyDefaults.Value.DefaultOpen;
    public static Fret One => _lazyDefaults.Value.DefaultOne;
    public static Fret Two => _lazyDefaults.Value.DefaultTwo;
    public static Fret Three => _lazyDefaults.Value.DefaultThree;
    public static Fret Four => _lazyDefaults.Value.DefaultFour;
    public static Fret Five => _lazyDefaults.Value.DefaultFive;

    public bool IsMuted => this == Muted;
    public bool IsOpen => this == Open;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fret FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new Fret { Value = value };
    }

    public static Fret Min => _lazyDefaults.Value.DefaultMin;
    public static Fret Max => _lazyDefaults.Value.DefaultMax;

    public static implicit operator Fret(int value)
    {
        return new Fret { Value = value };
    }

    public static implicit operator int(Fret fret)
    {
        return fret.Value;
    }

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
    public static Result<Fret, string> TryCreate(int value)
    {
        if (value is < _minValue or > _maxValue)
        {
            return Result<Fret, string>.Failure(
                $"Fret number must be between {_minValue} (muted) and {_maxValue}, got {value}");
        }

        return Result<Fret, string>.Success(new Fret { Value = value });
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<Fret>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<Fret>.EnsureValueInRange(value, minValue, maxValue);
    }

    public static IReadOnlyCollection<Fret> Range(int start, int count)
    {
        return ValueObjectUtils<Fret>.GetItems(start, count);
    }

    public static IReadOnlyCollection<Fret> Range(int start, int count, bool includeOpen)
    {
        return includeOpen ? ValueObjectUtils<Fret>.GetItemsWithHead(Open, start, count) : Range(start, count);
    }

    public static ImmutableSortedSet<Fret> Set(Range range)
    {
        return Set(Enumerable.Range(range.Start.Value, range.End.Value - range.Start.Value));
    }

    public static ImmutableSortedSet<Fret> Set(IEnumerable<int> values)
    {
        return values.Select(FromValue).ToImmutableSortedSet();
    }

    public static ImmutableSortedSet<Fret> Set(params int[] values)
    {
        return Set(values.AsEnumerable());
    }

    public static ImmutableSortedSet<Fret> Set(int value, Range range)
    {
        return Set(value, Enumerable.Range(range.Start.Value, range.End.Value - range.Start.Value + 1));
    }

    public static ImmutableSortedSet<Fret> Set(int value, params int[] values)
    {
        return Set(value, values.AsEnumerable());
    }

    public static ImmutableSortedSet<Fret> Set(int value, IEnumerable<int> values)
    {
        return Set(new[] { value }.Union(values));
    }

    public static Fret operator +(Fret fret, RelativeFret relativeFret)
    {
        return new Fret { Value = fret.Value + relativeFret.Value };
    }

    public void CheckMaxValue(int maxValue)
    {
        ValueObjectUtils<Fret>.EnsureValueRange(Value, _minValue, maxValue);
    }

    public override string ToString()
    {
        return _value switch
        {
            -1 => "x",
            0 => "O",
            _ => Value.ToString()
        };
    }

    private class Defaults
    {
        public Fret DefaultMin { get; } = FromValue(_minValue);
        public Fret DefaultMax { get; } = FromValue(_maxValue);
        public Fret DefaultMuted { get; } = FromValue(-1);
        public Fret DefaultOpen { get; } = FromValue(0);
        public Fret DefaultOne { get; } = FromValue(1);
        public Fret DefaultTwo { get; } = FromValue(2);
        public Fret DefaultThree { get; } = FromValue(3);
        public Fret DefaultFour { get; } = FromValue(4);
        public Fret DefaultFive { get; } = FromValue(5);
    }

    #region IStaticValueObjectList<Fret> Members

    public static IReadOnlyCollection<Fret> Items => ValueObjectUtils<Fret>.Items;
    public static IReadOnlyList<int> Values => ValueObjectUtils<Fret>.Values;

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

    public int CompareTo(Fret other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(Fret left, Fret right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(Fret left, Fret right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(Fret left, Fret right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Fret left, Fret right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
