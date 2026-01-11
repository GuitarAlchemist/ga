namespace GA.Business.Core.Tonal.Modes.Exotic;

using System;
using System.Collections.Generic;
using GA.Core.Collections.Abstractions;
using global::GA.Core.Collections;
using JetBrains.Annotations;
using Primitives.Exotic;
using Scales;

/// <summary>
///     A double harmonic scale mode (Byzantine scale)
/// </summary>
/// <remarks>
///     The double harmonic scale (also known as Byzantine or Arabic scale) has a flattened second and sixth degree.
///     It has seven distinct modes, each with unique characteristics.
///     It's commonly used in Middle Eastern and Balkan music.
///     <see href="https://en.wikipedia.org/wiki/Double_harmonic_scale" />
/// </remarks>
[PublicAPI]
public sealed class DoubleHarmonicScaleMode(DoubleHarmonicScaleDegree degree)
    : TonalScaleMode<DoubleHarmonicScaleDegree>(Scale.DoubleHarmonic, degree),
        IStaticEnumerable<DoubleHarmonicScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<DoubleHarmonicScaleDegree, DoubleHarmonicScaleMode>>
        _lazyModeByDegree = new(() => new([.. Items]));

    // Static instances for each mode
    public static DoubleHarmonicScaleMode DoubleHarmonic => new(DoubleHarmonicScaleDegree.DoubleHarmonic);

    public static DoubleHarmonicScaleMode LydianSharpSecondSharpSixth =>
        new(DoubleHarmonicScaleDegree.LydianSharpSecondSharpSixth);

    public static DoubleHarmonicScaleMode UltraPhrygian => new(DoubleHarmonicScaleDegree.UltraPhrygian);
    public static DoubleHarmonicScaleMode HungarianMinor => new(DoubleHarmonicScaleDegree.HungarianMinor);
    public static DoubleHarmonicScaleMode Oriental => new(DoubleHarmonicScaleDegree.Oriental);

    public static DoubleHarmonicScaleMode IonianAugmentedSharpSecond =>
        new(DoubleHarmonicScaleDegree.IonianAugmentedSharpSecond);

    public static DoubleHarmonicScaleMode LocrianDoubleFlat3DoubleFlat7 =>
        new(DoubleHarmonicScaleDegree.LocrianDoubleFlat3DoubleFlat7);

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    // Collection and access methods
    public static IEnumerable<DoubleHarmonicScaleMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<DoubleHarmonicScaleDegree>.Items)
            {
                yield return new(degree);
            }
        }
    }

    public static DoubleHarmonicScaleMode Get(DoubleHarmonicScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static DoubleHarmonicScaleMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }
}


