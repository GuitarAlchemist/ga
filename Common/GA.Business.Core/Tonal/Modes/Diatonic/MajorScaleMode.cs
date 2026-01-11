namespace GA.Business.Core.Tonal.Modes.Diatonic;

using System;
using System.Collections.Generic;
using GA.Core.Collections.Abstractions;
using global::GA.Core.Collections;
using JetBrains.Annotations;
using Primitives.Diatonic;
using Scales;

/// <summary>
///     A major scale mode
/// </summary>
/// <remarks>
///     Mnemonic: I Don't Particularly Like Modes A Lot => Ionian, Dorian, etc...
///     <see href="https://ianring.com/musictheory/scales/1709" />
/// </remarks>
[PublicAPI]
public sealed class MajorScaleMode(MajorScaleDegree degree) : TonalScaleMode<MajorScaleDegree>(Scale.Major, degree),
    IStaticEnumerable<MajorScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<MajorScaleDegree, MajorScaleMode>> _lazyModeByDegree =
        new(() => new([.. Items]));

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    // Collection and access methods
    public static IEnumerable<MajorScaleMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<MajorScaleDegree>.Items)
            {
                yield return new(degree);
            }
        }
    }

    public static MajorScaleMode FromDegree(MajorScaleDegree degree)
    {
        return new(degree);
    }

    public static MajorScaleMode Get(MajorScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static MajorScaleMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public override string ToString()
    {
        return $"{Name} - {Formula}";
    }
}


