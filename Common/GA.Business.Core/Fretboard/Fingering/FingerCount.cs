namespace GA.Business.Core.Fretboard.Fingering;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GA.Core.Abstractions;
using GA.Core.Collections;
using GA.Core.Collections.Abstractions;
using JetBrains.Annotations;

/// <inheritdoc cref="IEquatable{T}" />
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

    public static FingerCount Zero { get; } = FromValue(0);
    public static FingerCount One { get; } = FromValue(1);
    public static FingerCount Two { get; } = FromValue(2);
    public static FingerCount Three { get; } = FromValue(3);
    public static FingerCount Four { get; } = FromValue(4);
    public static FingerCount Five { get; } = FromValue(5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FingerCount FromValue([ValueRange(_minValue, _maxValue)] int value) =>
        new() { Value = value };

    public static FingerCount Min { get; } = FromValue(_minValue);
    public static FingerCount Max { get; } = FromValue(_maxValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator FingerCount(int value) => new() { Value = value };
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator int(FingerCount fingerCount) => fingerCount.Value;

    public static int CheckRange(int value) =>
        IRangeValueObject<FingerCount>.EnsureValueInRange(value, _minValue, _maxValue);

    public static int CheckRange(int value, int minValue, int maxValue) =>
        IRangeValueObject<FingerCount>.EnsureValueInRange(value, minValue, maxValue);

    public void CheckMaxValue(int maxValue) =>
        ValueObjectUtils<FingerCount>.EnsureValueRange(Value, _minValue, maxValue);

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();

    #region IStaticValueObjectList<FingerCount> Members

    /// <summary>
    /// Gets all FingerCount instances (automatically memoized).
    /// </summary>
    public static IReadOnlyCollection<FingerCount> Items => ValueObjectUtils<FingerCount>.Items;

    /// <summary>
    /// Gets all FingerCount values (automatically memoized).
    /// </summary>
    public static IReadOnlyList<int> Values => ValueObjectUtils<FingerCount>.Values;

    /// <summary>
    /// Gets the cached span representing the full finger count range.
    /// </summary>
    public static ReadOnlySpan<FingerCount> ItemsSpan => ValueObjectUtils<FingerCount>.ItemsSpan;

    /// <summary>
    /// Gets the cached span representing the numeric values for each finger count.
    /// </summary>
    public static ReadOnlySpan<int> ValuesSpan => ValueObjectUtils<FingerCount>.ValuesSpan;

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
