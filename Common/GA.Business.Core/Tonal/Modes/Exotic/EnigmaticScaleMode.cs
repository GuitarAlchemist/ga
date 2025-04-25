﻿namespace GA.Business.Core.Tonal.Modes.Exotic;

using Scales;
using Primitives;
using Primitives.Exotic;

/// <summary>
/// An enigmatic scale mode
/// </summary>
/// <remarks>
/// The enigmatic scale was created by Giuseppe Verdi and has a unique and mysterious sound.
/// It consists of a semitone, three major thirds, and a minor second.
/// It's used in modern classical music and film scoring.
///
/// <see href="https://en.wikipedia.org/wiki/Enigmatic_scale"/>
/// </remarks>
[PublicAPI]
public sealed class EnigmaticScaleMode(EnigmaticScaleDegree degree) : TonalScaleMode<EnigmaticScaleDegree>(Scale.Enigmatic, degree),
    IStaticEnumerable<EnigmaticScaleMode>
{
    // Static instances for each mode
    public static EnigmaticScaleMode Enigmatic => new(EnigmaticScaleDegree.Enigmatic);
    public static EnigmaticScaleMode EnigmaticDorian => new(EnigmaticScaleDegree.EnigmaticDorian);
    public static EnigmaticScaleMode EnigmaticPhrygian => new(EnigmaticScaleDegree.EnigmaticPhrygian);
    public static EnigmaticScaleMode EnigmaticLydian => new(EnigmaticScaleDegree.EnigmaticLydian);
    public static EnigmaticScaleMode EnigmaticMixolydian => new(EnigmaticScaleDegree.EnigmaticMixolydian);
    public static EnigmaticScaleMode EnigmaticAeolian => new(EnigmaticScaleDegree.EnigmaticAeolian);
    public static EnigmaticScaleMode EnigmaticLocrian => new(EnigmaticScaleDegree.EnigmaticLocrian);

    // Collection and access methods
    public static IEnumerable<EnigmaticScaleMode> Items => EnigmaticScaleDegree.Items.Select(degree => new EnigmaticScaleMode(degree));
    public static EnigmaticScaleMode Get(EnigmaticScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static EnigmaticScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<EnigmaticScaleDegree, EnigmaticScaleMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    // Properties
    public override string Name => ParentScaleDegree.ToName();
}
