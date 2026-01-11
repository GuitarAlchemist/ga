namespace GA.Business.Core.Tonal.Modes.Diatonic;

using System;
using System.Collections.Generic;
using GA.Core.Collections.Abstractions;
using global::GA.Core.Collections;
using JetBrains.Annotations;
using Primitives.Diatonic;
using Scales;

/// <summary>
///     A natural minor scale mode
/// </summary>
/// <remarks>
///     The natural minor scale is also known as the Aeolian mode.
/// </remarks>
[PublicAPI]
public sealed class NaturalMinorMode(NaturalMinorScaleDegree degree) : MinorScaleMode<NaturalMinorScaleDegree>(
        Scale.NaturalMinor, degree),
    IStaticEnumerable<NaturalMinorMode>
{
    private static readonly Lazy<ScaleModeCollection<NaturalMinorScaleDegree, NaturalMinorMode>> _lazyModeByDegree =
        new(() => new([.. Items]));

    public static NaturalMinorMode Aeolian => new(1);
    public static NaturalMinorMode Locrian => new(2);
    public static NaturalMinorMode Ionian => new(3);
    public static NaturalMinorMode Dorian => new(4);
    public static NaturalMinorMode Phrygian => new(5);
    public static NaturalMinorMode Lydian => new(6);
    public static NaturalMinorMode Mixolydian => new(7);

    public override string Name => ParentScaleDegree.Value switch
    {
        1 => nameof(Aeolian),
        2 => nameof(Locrian),
        3 => nameof(Ionian),
        4 => nameof(Dorian),
        5 => nameof(Phrygian),
        6 => nameof(Lydian),
        7 => nameof(Mixolydian),
        _ => throw new ArgumentOutOfRangeException(nameof(ParentScaleDegree))
    };


    public static IEnumerable<NaturalMinorMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<NaturalMinorScaleDegree>.Items)
            {
                yield return new(degree);
            }
        }
    }

    public static NaturalMinorMode Get(NaturalMinorScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static NaturalMinorMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public override string ToString()
    {
        return $"{Name} - {Formula}";
    }
}


