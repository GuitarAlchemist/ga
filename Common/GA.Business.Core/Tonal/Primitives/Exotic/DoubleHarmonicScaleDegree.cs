namespace GA.Business.Core.Tonal.Primitives.Exotic;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GA.Core.Abstractions;
using GA.Core.Collections;
using JetBrains.Annotations;

/// <summary>
///     A double harmonic scale degree (Byzantine scale)
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/Double_harmonic_scale" />
/// </remarks>
[PublicAPI]
public readonly record struct DoubleHarmonicScaleDegree : IRangeValueObject<DoubleHarmonicScaleDegree>,
    IScaleDegreeNaming
{
    private const int _minValue = 1;
    private const int _maxValue = 7;

    private readonly int _value;

    // Constructor
    public DoubleHarmonicScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static IReadOnlyCollection<DoubleHarmonicScaleDegree> All =>
        ValueObjectUtils<DoubleHarmonicScaleDegree>.Items;

    public static IReadOnlyCollection<DoubleHarmonicScaleDegree> Items =>
        ValueObjectUtils<DoubleHarmonicScaleDegree>.Items;

    public static IReadOnlyCollection<int> Values => [.. Items.Select(degree => degree.Value)];

    // Static instances for convenience
    public static DoubleHarmonicScaleDegree DoubleHarmonic => new(1);
    public static DoubleHarmonicScaleDegree LydianSharpSecondSharpSixth => new(2);
    public static DoubleHarmonicScaleDegree UltraPhrygian => new(3);
    public static DoubleHarmonicScaleDegree HungarianMinor => new(4);
    public static DoubleHarmonicScaleDegree Oriental => new(5);
    public static DoubleHarmonicScaleDegree IonianAugmentedSharpSecond => new(6);
    public static DoubleHarmonicScaleDegree LocrianDoubleFlat3DoubleFlat7 => new(7);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DoubleHarmonicScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value)
    {
        return new()
            { Value = value };
    }

    public static DoubleHarmonicScaleDegree Min => FromValue(_minValue);
    public static DoubleHarmonicScaleDegree Max => FromValue(_maxValue);

    public static implicit operator DoubleHarmonicScaleDegree(int value)
    {
        return FromValue(value);
    }

    public static implicit operator int(DoubleHarmonicScaleDegree degree)
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
            1 => "Double harmonic",
            2 => "Lydian #2 #6",
            3 => "Ultra Phrygian",
            4 => "Hungarian minor",
            5 => "Oriental",
            6 => "Ionian augmented #2",
            7 => "Locrian bb3 bb7",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public string ToShortName()
    {
        return Value switch
        {
            1 => "DH",
            2 => "Lyd#2#6",
            3 => "UPhr",
            4 => "HunMin",
            5 => "Orient",
            6 => "Ion+#2",
            7 => "Locbb3bb7",
            _ => throw new ArgumentOutOfRangeException(nameof(Value))
        };
    }

    public static int CheckRange(int value)
    {
        return IRangeValueObject<DoubleHarmonicScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    }

    public static int CheckRange(int value, int minValue, int maxValue)
    {
        return IRangeValueObject<DoubleHarmonicScaleDegree>.EnsureValueInRange(value, minValue, maxValue);
    }

    public override string ToString()
    {
        return Value.ToString();
    }

    #region Relational members

    public int CompareTo(DoubleHarmonicScaleDegree other)
    {
        return _value.CompareTo(other._value);
    }

    public static bool operator <(DoubleHarmonicScaleDegree left, DoubleHarmonicScaleDegree right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(DoubleHarmonicScaleDegree left, DoubleHarmonicScaleDegree right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(DoubleHarmonicScaleDegree left, DoubleHarmonicScaleDegree right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(DoubleHarmonicScaleDegree left, DoubleHarmonicScaleDegree right)
    {
        return left.CompareTo(right) >= 0;
    }

    #endregion
}
