﻿namespace GA.Business.Core.Tonal.Primitives.Exotic;

/// <summary>
/// A bebop scale degree
/// </summary>
/// <remarks>
/// <see href="https://en.wikipedia.org/wiki/Bebop_scale"/>
/// </remarks>
[PublicAPI]
public readonly record struct BebopScaleDegree : IRangeValueObject<BebopScaleDegree>, IScaleDegreeNaming
{
    #region Relational members

    public int CompareTo(BebopScaleDegree other) => _value.CompareTo(other._value);
    public static bool operator <(BebopScaleDegree left, BebopScaleDegree right) => left.CompareTo(right) < 0;
    public static bool operator >(BebopScaleDegree left, BebopScaleDegree right) => left.CompareTo(right) > 0;
    public static bool operator <=(BebopScaleDegree left, BebopScaleDegree right) => left.CompareTo(right) <= 0;
    public static bool operator >=(BebopScaleDegree left, BebopScaleDegree right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = 1;
    private const int _maxValue = 8;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static BebopScaleDegree FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    // Constructor
    public BebopScaleDegree(int value)
    {
        _value = CheckRange(value);
    }

    public static BebopScaleDegree Min => FromValue(_minValue);
    public static BebopScaleDegree Max => FromValue(_maxValue);

    public static int CheckRange(int value) => IRangeValueObject<BebopScaleDegree>.EnsureValueInRange(value, _minValue, _maxValue);
    public static int CheckRange(int value, int minValue, int maxValue) => IRangeValueObject<BebopScaleDegree>.EnsureValueInRange(value, minValue, maxValue);

    public static implicit operator BebopScaleDegree(int value) => FromValue(value);
    public static implicit operator int(BebopScaleDegree degree) => degree.Value;

    public static IReadOnlyCollection<BebopScaleDegree> All => ValueObjectUtils<BebopScaleDegree>.Items;
    public static IReadOnlyCollection<BebopScaleDegree> Items => ValueObjectUtils<BebopScaleDegree>.Items;
    public static IReadOnlyCollection<int> Values => Items.Select(degree => degree.Value).ToImmutableList();

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    // Static instances for convenience
    public static BebopScaleDegree BebopDominant => new(1);
    public static BebopScaleDegree BebopMajor => new(2);
    public static BebopScaleDegree BebopDorian => new(3);
    public static BebopScaleDegree BebopMinor => new(4);
    public static BebopScaleDegree BebopMelodic => new(5);
    public static BebopScaleDegree BebopHarmonic => new(6);
    public static BebopScaleDegree BebopLocrian => new(7);
    public static BebopScaleDegree BebopDiminished => new(8);

    public override string ToString() => Value.ToString();

    public string ToName() => Value switch
    {
        1 => "Bebop dominant",
        2 => "Bebop major",
        3 => "Bebop Dorian",
        4 => "Bebop minor",
        5 => "Bebop melodic",
        6 => "Bebop harmonic",
        7 => "Bebop Locrian",
        8 => "Bebop diminished",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };

    public string ToShortName() => Value switch
    {
        1 => "BebDom",
        2 => "BebMaj",
        3 => "BebDor",
        4 => "BebMin",
        5 => "BebMel",
        6 => "BebHar",
        7 => "BebLoc",
        8 => "BebDim",
        _ => throw new ArgumentOutOfRangeException(nameof(Value))
    };
}
