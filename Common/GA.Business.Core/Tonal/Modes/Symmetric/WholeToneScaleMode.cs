namespace GA.Business.Core.Tonal.Modes.Symmetric;

using Scales;
using Primitives;
using Primitives.Symmetric;

/// <summary>
/// A whole tone scale mode
/// </summary>
/// <remarks>
/// The whole tone scale is a hexatonic scale consisting of whole tones.
/// Due to its symmetrical structure, it has only two distinct modes.
///
/// <see href="https://en.wikipedia.org/wiki/Whole-tone_scale"/>
/// </remarks>
[PublicAPI]
public sealed class WholeToneScaleMode(WholeToneScaleDegree degree) : SymmetricScaleMode<WholeToneScaleDegree>(Scale.WholeTone, degree),
    IStaticEnumerable<WholeToneScaleMode>
{
    // Static instances for each mode
    public static WholeToneScaleMode WholeTone => new(WholeToneScaleDegree.WholeTone);
    public static WholeToneScaleMode WholeTone2 => new(WholeToneScaleDegree.WholeTone2);

    // Collection and access methods
    public static IEnumerable<WholeToneScaleMode> Items => WholeToneScaleDegree.Items.Select(degree => new WholeToneScaleMode(degree));
    public static WholeToneScaleMode Get(WholeToneScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static WholeToneScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static readonly Lazy<ScaleModeCollection<WholeToneScaleDegree, WholeToneScaleMode>> _lazyModeByDegree = new(() => new(Items.ToImmutableList()));

    // Properties
    public override string Name => ParentScaleDegree.ToName();
    
    /// <summary>
    /// Gets a value indicating whether this scale has limited transpositions.
    /// </summary>
    /// <remarks>
    /// The whole-tone scale has only 2 distinct transpositions.
    /// </remarks>
    public override bool HasLimitedTranspositions => true;
    
    /// <summary>
    /// Gets the number of distinct transpositions this scale has.
    /// </summary>
    /// <remarks>
    /// The whole-tone scale has only 2 distinct transpositions.
    /// </remarks>
    public override int TranspositionCount => 2;
}
