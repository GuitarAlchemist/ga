namespace GA.Business.Core.Tonal.Modes.Exotic;

using Scales;
using Primitives;
using Primitives.Exotic;

/// <summary>
/// A Neapolitan major scale mode
/// </summary>
/// <remarks>
/// The Neapolitan major scale is a major scale with a lowered 2nd degree.
/// It has seven distinct modes, each with unique characteristics.
/// It's used in classical music and film scoring.
///
/// <see href="https://en.wikipedia.org/wiki/Neapolitan_scale"/>
/// </remarks>
[PublicAPI]
public sealed class NeapolitanMajorScaleMode(NeapolitanMajorScaleDegree degree) : TonalScaleMode<NeapolitanMajorScaleDegree>(Scale.NeapolitanMajor, degree),
    IStaticEnumerable<NeapolitanMajorScaleMode>
{
    // Static instances for each mode
    public static NeapolitanMajorScaleMode NeapolitanMajor => new(NeapolitanMajorScaleDegree.NeapolitanMajor);
    public static NeapolitanMajorScaleMode LeadingWholeTone => new(NeapolitanMajorScaleDegree.LeadingWholeTone);
    public static NeapolitanMajorScaleMode LydianAugmentedDominant => new(NeapolitanMajorScaleDegree.LydianAugmentedDominant);
    public static NeapolitanMajorScaleMode LydianDominantFlat6 => new(NeapolitanMajorScaleDegree.LydianDominantFlat6);  
    public static NeapolitanMajorScaleMode MajorLocrian => new(NeapolitanMajorScaleDegree.MajorLocrian);
    public static NeapolitanMajorScaleMode SemiLocrianFlat4 => new(NeapolitanMajorScaleDegree.SemiLocrianFlat4);        
    public static NeapolitanMajorScaleMode SuperLocrianDoubleFlat7 => new(NeapolitanMajorScaleDegree.SuperLocrianDoubleFlat7);

    // Collection and access methods
    public static IEnumerable<NeapolitanMajorScaleMode> Items => NeapolitanMajorScaleDegree.Items.Select(degree => new NeapolitanMajorScaleMode(degree));
    public static NeapolitanMajorScaleMode Get(NeapolitanMajorScaleDegree degree) => _lazyModeByDegree.Value[degree];   
    public static NeapolitanMajorScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<NeapolitanMajorScaleDegree, NeapolitanMajorScaleMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    // Properties
    public override string Name => ParentScaleDegree.ToName();
}
