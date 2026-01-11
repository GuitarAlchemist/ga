namespace GA.Business.Core.Tonal.Modes.Exotic;

using System;
using System.Collections.Generic;
using GA.Core.Collections.Abstractions;
using global::GA.Core.Collections;
using JetBrains.Annotations;
using Primitives.Exotic;
using Scales;

/// <summary>
///     A blues scale mode
/// </summary>
/// <remarks>
///     The blues scale is a six-note scale commonly used in blues, jazz, and rock music.
///     It consists of the root, flat third, fourth, flat fifth, fifth, and flat seventh.
///     <see href="https://en.wikipedia.org/wiki/Blues_scale" />
/// </remarks>
[PublicAPI]
public sealed class BluesScaleMode(BluesScaleDegree degree) : TonalScaleMode<BluesScaleDegree>(Scale.Blues, degree),
    IStaticEnumerable<BluesScaleMode>
{
    private static readonly Lazy<ScaleModeCollection<BluesScaleDegree, BluesScaleMode>> _lazyModeByDegree =
        new(() => new([.. Items]));

    // Static instances for each mode
    public static BluesScaleMode Blues => new(BluesScaleDegree.Blues);
    public static BluesScaleMode MinorBlues => new(BluesScaleDegree.MinorBlues);
    public static BluesScaleMode BluesPhrygian => new(BluesScaleDegree.BluesPhrygian);
    public static BluesScaleMode BluesDorian => new(BluesScaleDegree.BluesDorian);
    public static BluesScaleMode BluesMixolydian => new(BluesScaleDegree.BluesMixolydian);
    public static BluesScaleMode BluesAeolian => new(BluesScaleDegree.BluesAeolian);

    // Properties
    public override string Name => ParentScaleDegree.ToName();

    // Collection and access methods
    public static IEnumerable<BluesScaleMode> Items
    {
        get
        {
            foreach (var degree in ValueObjectUtils<BluesScaleDegree>.Items)
            {
                yield return new(degree);
            }
        }
    }

    public static BluesScaleMode Get(BluesScaleDegree degree)
    {
        return _lazyModeByDegree.Value[degree];
    }

    public static BluesScaleMode Get(int degree)
    {
        return _lazyModeByDegree.Value[degree];
    }
}


