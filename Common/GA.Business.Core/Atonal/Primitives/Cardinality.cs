namespace GA.Business.Core.Atonal.Primitives;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using GA.Core.Abstractions;
using GA.Core.Collections;
using GA.Core.Collections.Abstractions;
using GA.Core.Functional;
using JetBrains.Annotations;

/// <summary>
///     Cardinality is the count of unique pitch classes in a pitch class set
/// </summary>
/// <remarks>
///     Implements <see cref="IStaticValueObjectList{TSelf}" />, <see cref="IName" />
/// </remarks>
[PublicAPI]
public readonly record struct Cardinality : IStaticReadonlyCollectionFromValues<Cardinality>,
    IName
{
    private static readonly ImmutableDictionary<int, string> _cardinalityNames = new Dictionary<int, string>
    {
        [0] = string.Empty,
        [1] = "Monotonic",
        [2] = "Ditonic",
        [3] = "Tritonic",
        [4] = "Tetratonic",
        [5] = "Pentatonic",
        [6] = "Hexatonic",
        [7] = "Heptatonic",
        [8] = "Octatonic",
        [9] = "Enneatonic",
        [10] = "Decatonic",
        [11] = "Hendecatonic",
        [12] = "Dodecatonic"
    }.ToImmutableDictionary();

    /// <inheritdoc cref="IName.Name" />
    public string Name => _cardinalityNames[_value];

    /// <inheritdoc />
    public override string ToString()
    {
        return string.IsNullOrEmpty(Name) ? Value.ToString() : $"{Value} ({Name})";
    }

    #region IStaticValueObjectList<Cardinality> Members

    public static Cardinality Min => FromValue(_minValue);
    public static Cardinality Max => FromValue(_maxValue);

    public static IReadOnlyCollection<Cardinality> Items => IStaticReadonlyCollectionFromValues<Cardinality>.Items;

    /// <summary>
    /// Gets the cached span representing the full cardinality range.
    /// </summary>
    public static ReadOnlySpan<Cardinality> ItemsSpan => ValueObjectUtils<Cardinality>.ItemsSpan;

    /// <summary>
    /// Gets the cached span representing the numeric values for each cardinality.
    /// </summary>
    public static ReadOnlySpan<int> ValuesSpan => ValueObjectUtils<Cardinality>.ValuesSpan;

    private const int _minValue = 0;
    private const int _maxValue = 12;

    /// <summary>
    ///     Creates a new Cardinality from an int value with range validation.
    /// </summary>
    /// <param name="value">
    ///     The cardinality (number of unique pitch classes). Must be between <see cref="Min" /> (0) and
    ///     <see cref="Max" /> (12).
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value" /> is outside the valid range [0..12].</exception>
    /// <remarks>
    ///     You can also use implicit conversion: <c>Cardinality card = 5;</c> for pentatonic sets.
    /// </remarks>
    public Cardinality([ValueRange(_minValue, _maxValue)] int value)
    {
        _value = CheckRange(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Cardinality FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new()
            { Value = value };
    }

    /// <summary>
    ///     Attempts to create a Cardinality from an int value, returning a Result instead of throwing.
    /// </summary>
    /// <param name="value">The cardinality value to validate.</param>
    /// <returns>A Result containing either a valid Cardinality or an error message.</returns>
    /// <remarks>
    ///     This method enables functional error handling without exceptions.
    ///     Example:
    ///     <code>
    /// var result = Cardinality.TryCreate(userInput)
    ///     .Map(card => card.Value)
    ///     .Match(
    ///         onSuccess: v => $"Valid cardinality: {v}",
    ///         onFailure: err => $"Error: {err}"
    ///     );
    /// </code>
    /// </remarks>
    public static Result<Cardinality, string> TryCreate(int value)
    {
        if (value is < _minValue or > _maxValue)
        {
            return Result<Cardinality, string>.Failure(
                $"Cardinality must be between {_minValue} and {_maxValue}, got {value}");
        }

        return Result<Cardinality, string>.Success(new()
            { Value = value });
    }

    public static implicit operator Cardinality(int value)
    {
        return new()
            { Value = value };
    }

    public static implicit operator int(Cardinality fret)
    {
        return fret.Value;
    }

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    private readonly int _value;

    private static int CheckRange(int value)
    {
        return IRangeValueObject<Cardinality>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    #endregion

    #region Relational members

    public int CompareTo(Cardinality other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(Cardinality left, Cardinality right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(Cardinality left, Cardinality right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(Cardinality left, Cardinality right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Cardinality left, Cardinality right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
