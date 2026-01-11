namespace GA.Business.Core.Tonal.Modes.Exotic;

using System;
using System.Collections.Generic;
using GA.Core.Collections.Abstractions;
using global::GA.Core.Collections;
using JetBrains.Annotations;
using Primitives.Exotic;
using Scales;

/// <summary>
///     A Neapolitan minor scale mode
/// </summary>
/// <remarks>
///     The Neapolitan minor scale is a minor scale with a lowered 2nd degree.
///     It has seven distinct modes, each with unique characteristics.
///     It's used in classical music and film scoring.
///     <see href="https://en.wikipedia.org/wiki/Neapolitan_scale" />
/// </remarks>
[PublicAPI]
public sealed class NeapolitanMinorScaleMode(NeapolitanMinorScaleDegree degree)
    : TonalScaleMode<NeapolitanMinorScaleDegree>(Scale.NeapolitanMinor, degree),
        IStaticEnumerable<NeapolitanMinorScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<NeapolitanMinorScaleDegree, NeapolitanMinorScaleMode>>
        _lazyModeByDegree = new(() => new([.. Items]));

    // Static instances for each mode
    public static NeapolitanMinorScaleMode NeapolitanMinor => new(NeapolitanMinorScaleDegree.NeapolitanMinor);
    public static NeapolitanMinorScaleMode LydianSharp2 => new(NeapolitanMinorScaleDegree.LydianSharp2);
    public static NeapolitanMinorScaleMode MixolydianAugmented => new(NeapolitanMinorScaleDegree.MixolydianAugmented);
    public static NeapolitanMinorScaleMode HungarianGypsy => new(NeapolitanMinorScaleDegree.HungarianGypsy);
    public static NeapolitanMinorScaleMode LocrianDominant => new(NeapolitanMinorScaleDegree.LocrianDominant);
    public static NeapolitanMinorScaleMode IonianSharp2Sharp5 => new(NeapolitanMinorScaleDegree.IonianSharp2Sharp5);
    public static NeapolitanMinorScaleMode UltraLocrianbb3 => new(NeapolitanMinorScaleDegree.UltraLocrianbb3);

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    // Collection and access methods
    public static IEnumerable<NeapolitanMinorScaleMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<NeapolitanMinorScaleDegree>.Items)
            {
                yield return new(degree);
            }
        }
    }

    public static NeapolitanMinorScaleMode Get(NeapolitanMinorScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static NeapolitanMinorScaleMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }
}


