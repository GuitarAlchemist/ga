namespace GA.Business.Core.Tonal.Modes.Diatonic;

using System;
using System.Collections.Generic;
using GA.Core.Collections.Abstractions;
using global::GA.Core.Collections;
using JetBrains.Annotations;
using Primitives.Diatonic;
using Scales;

/// <summary>
///     A melodic minor scale mode
/// </summary>
/// <remarks>
///     The melodic minor scale is a minor scale with raised 6th and 7th degrees when ascending.
/// </remarks>
[PublicAPI]
public sealed class MelodicMinorMode(MelodicMinorScaleDegree degree) : MinorScaleMode<MelodicMinorScaleDegree>(
        Scale.MelodicMinor, degree),
    IStaticEnumerable<MelodicMinorMode>
{
    // Cached singletons to avoid repeated allocations
    private static readonly Lazy<ScaleModeCollection<MelodicMinorScaleDegree, MelodicMinorMode>> _lazyModeByDegree =
        new(() => new([.. Items]));

    public static MelodicMinorMode MelodicMinorModeMinor { get; } = new(1);

    public static MelodicMinorMode DorianFlatSecond { get; } = new(2);

    public static MelodicMinorMode LydianAugmented { get; } = new(3);

    public static MelodicMinorMode LydianDominant { get; } = new(4);

    public static MelodicMinorMode MixolydianFlatSixth { get; } = new(5);

    public static MelodicMinorMode LocrianNaturalSecond { get; } = new(6);

    public static MelodicMinorMode Altered { get; } = new(7);

    public override string Name => ParentScaleDegree.Value switch
    {
        1 => "Melodic minor",
        2 => "Dorian \u266D2",
        3 => "Lydian \u266F5",
        4 => "Lydian dominant",
        5 => "Mixolydian \u266D6",
        6 => "Locrian \u266E2",
        7 => "Altered",
        _ => throw new ArgumentOutOfRangeException(nameof(ParentScaleDegree))
    };

    public static IEnumerable<MelodicMinorMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<MelodicMinorScaleDegree>.Items)
            {
                yield return new(degree);
            }
        }
    }

    public static MelodicMinorMode Get(MelodicMinorScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static MelodicMinorMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public override string ToString()
    {
        return $"{Name} - {Formula}";
    }
}


