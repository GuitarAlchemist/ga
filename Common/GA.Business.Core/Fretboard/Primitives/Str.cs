namespace GA.Business.Core.Fretboard.Primitives;

/// <summary>
///     An instrument string (Between <see cref="Min" /> and <see cref="Max" />)
/// </summary>
/// <remarks>
///     String 1 is the string with the highest pitch.
/// </remarks>
[PublicAPI]
public readonly record struct Str : IRangeValueObject<Str>
{
    private const int _minValue = 1;
    private const int _maxValue = 26;

    private readonly int _value;

    /// <summary>
    ///     Creates a new Str from an int value with range validation.
    /// </summary>
    /// <param name="value">The string number. Must be between <see cref="Min" /> (1) and <see cref="Max" /> (26).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value" /> is outside the valid range [1..26].</exception>
    /// <remarks>
    ///     String 1 is the highest pitch string. You can also use implicit conversion: <c>Str str = 3;</c>
    /// </remarks>
    public Str([ValueRange(_minValue, _maxValue)] int value)
    {
        _value = CheckRange(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Str FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new Str { Value = value };
    }

    /// <summary>
    ///     The first string (Highest pitch)
    /// </summary>
    public static Str Min => FromValue(_minValue);

    /// <summary>
    ///     The last string (Lowest pitch)
    /// </summary>
    public static Str Max => FromValue(_maxValue);

    public static implicit operator Str(int value)
    {
        return new Str { Value = value };
    }

    public static implicit operator int(Str str)
    {
        return str._value;
    }

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    /// <summary>
    ///     Attempts to create a Str from an int value, returning a Result instead of throwing.
    /// </summary>
    /// <param name="value">The string number to validate.</param>
    /// <returns>A Result containing either a valid Str or an error message.</returns>
    /// <remarks>
    ///     This method enables functional error handling without exceptions.
    ///     Example:
    ///     <code>
    /// var result = Str.TryCreate(userInput)
    ///     .Map(str => str.Value)
    ///     .Match(
    ///         onSuccess: v => $"Valid string: {v}",
    ///         onFailure: err => $"Error: {err}"
    ///     );
    /// </code>
    /// </remarks>
    public static Result<Str, string> TryCreate(int value)
    {
        if (value is < _minValue or > _maxValue)
        {
            return Result<Str, string>.Failure(
                $"String number must be between {_minValue} and {_maxValue}, got {value}");
        }

        return Result<Str, string>.Success(new Str { Value = value });
    }

    public static Str operator ++(Str str)
    {
        return FromValue(str._value + 1);
    }

    public static int CheckRange(int value)
    {
        return ValueObjectUtils<Str>.EnsureValueRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return ValueObjectUtils<Str>.EnsureValueRange(value, minValue, maxValue);
    }

    public static IReadOnlyCollection<Str> Range(int count)
    {
        return ValueObjectUtils<Str>.GetItems(_minValue, count);
    }

    public void CheckMaxValue(int maxValue)
    {
        ValueObjectUtils<Str>.EnsureValueRange(Value, _minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(Str other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(Str left, Str right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(Str left, Str right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(Str left, Str right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Str left, Str right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
