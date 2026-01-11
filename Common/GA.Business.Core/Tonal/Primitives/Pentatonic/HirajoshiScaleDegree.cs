namespace GA.Business.Core.Tonal.Primitives.Pentatonic;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GA.Core.Abstractions;
using GA.Core.Collections;
using JetBrains.Annotations;

/// <summary>
///     A Hirajoshi scale degree (Japanese pentatonic scale)
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/Hirajoshi_scale" />
/// </remarks>
[PublicAPI]
public readonly record struct HirajoshiScaleDegree : IRangeValueObject<HirajoshiScaleDegree>, IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 5;

    private readonly int _value;

    // Constructor
    public HirajoshiScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<HirajoshiScaleDegree> All => ValueObjectUtils<HirajoshiScaleDegree>.Items;
    public static IReadOnlyCollection<HirajoshiScaleDegree> Items => ValueObjectUtils<HirajoshiScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => [.. Items.Select(degree => degree.Value)];

    // Static instances for convenience
    public static HirajoshiScaleDegree Hirajoshi => new(1);
    public static HirajoshiScaleDegree HirajoshiKumoi => new(2);
    public static HirajoshiScaleDegree HirajoshiHonKumoi => new(3);
    public static HirajoshiScaleDegree HirajoshiIwato => new(4);
    public static HirajoshiScaleDegree HirajoshiAkebono => new(5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static HirajoshiScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new()
            { Value = value };
    }

    public static HirajoshiScaleDegree Min => FromValue(_minValue);
    public static HirajoshiScaleDegree Max => FromValue(_maxValue);

    public static implicit operator HirajoshiScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(HirajoshiScaleDegree degree)
    {
        return degree.Value;
    }

    public int Value
    {
        get => _value;
        init => _value = CheckRange(value);
    }

    public string ToName()
    {
        return Value switch
        {
            1 => "Hirajoshi",
            2 => "Kumoi",
            3 => "Hon-kumoi",
            4 => "Iwato",
            5 => "Akebono",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
        {
            1 => "Hira",
            2 => "Kumoi",
            3 => "HonK",
            4 => "Iwato",
            5 => "Akeb",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<HirajoshiScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<HirajoshiScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(HirajoshiScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(HirajoshiScaleDegree left, HirajoshiScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(HirajoshiScaleDegree left, HirajoshiScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(HirajoshiScaleDegree left, HirajoshiScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(HirajoshiScaleDegree left, HirajoshiScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
