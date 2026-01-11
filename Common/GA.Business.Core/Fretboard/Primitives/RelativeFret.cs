namespace GA.Business.Core.Fretboard.Primitives;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GA.Core.Abstractions;
using GA.Core.Collections;
using GA.Core.Collections.Abstractions;
using JetBrains.Annotations;

/// <inheritdoc cref="IEquatable{T}" />
/// <inheritdoc cref="IComparable{RelativeFret}" />
/// <inheritdoc cref="IComparable" />
/// <summary>
///     A positive distance between two frets (Ranges from <see cref="Min" /> to <see cref="Max" />)
/// </summary>
[PublicAPI]
public readonly record struct RelativeFret : IStaticValueObjectList<RelativeFret>
{
    private const int _minValue = 0;
    private const int _maxValue = 36;

    public static RelativeFret Zero { get; } = FromValue(0);

    public static RelativeFret One { get; } = FromValue(1);

    public static RelativeFret Two { get; } = FromValue(2);

    public static RelativeFret Three { get; } = FromValue(3);

    public static RelativeFret Four { get; } = FromValue(4);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RelativeFret FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new()
            { Value = value };
    }

    public static RelativeFret Min { get; } = FromValue(_minValue);

    public static RelativeFret Max { get; } = FromValue(_maxValue);

    public static implicit operator RelativeFret(int value)
    {
        return new()
            { Value = value };
    }

    public static implicit operator int(RelativeFret relativeFret)
    {
        return relativeFret.Value;
    }

    public static int CheckRange(int value) =>
        IRangeValueObject<RelativeFret>.EnsureValueInRange(value, _minValue, _maxValue);

    public static int CheckRange(int value, int minValue, int maxValue) =>
        IRangeValueObject<RelativeFret>.EnsureValueInRange(value, minValue, maxValue);

    public static IReadOnlyCollection<RelativeFret> Range(int start, int count) =>
        ValueObjectUtils<RelativeFret>.GetItems(start, count);

    public void CheckMaxValue(int maxValue)
    {
        ValueObjectUtils<RelativeFret>.EnsureValueRange(Value, _minValue, maxValue);
    }

    public override string ToString()
    {
        return _value.ToString();
    }

    #region IStaticValueObjectList<RelativeFret> Members

    /// <summary>
    /// Gets all RelativeFret instances (automatically memoized).
    /// </summary>
    public static IReadOnlyCollection<RelativeFret> Items => ValueObjectUtils<RelativeFret>.Items;

    /// <summary>
    /// Gets all RelativeFret values (automatically memoized).
    /// </summary>
    public static IReadOnlyList<int> Values => ValueObjectUtils<RelativeFret>.Values;

    /// <summary>
    /// Gets the cached span representing the full relative fret range.
    /// </summary>
    public static ReadOnlySpan<RelativeFret> ItemsSpan => ValueObjectUtils<RelativeFret>.ItemsSpan;

    /// <summary>
    /// Gets the cached span representing the numeric values for each relative fret.
    /// </summary>
    public static ReadOnlySpan<int> ValuesSpan => ValueObjectUtils<RelativeFret>.ValuesSpan;

    #endregion

    #region IValueObject<RelativeFret>

    private readonly int _value;

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    #endregion

    #region Relational members

    public int CompareTo(RelativeFret other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(RelativeFret left, RelativeFret right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(RelativeFret left, RelativeFret right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(RelativeFret left, RelativeFret right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(RelativeFret left, RelativeFret right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
