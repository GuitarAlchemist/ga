namespace GA.Domain.Core.Theory.Harmony;

using Tonal.Modes;

/// <summary>
///     Extension methods for working with ChordTemplate discriminated union
/// </summary>
public static class ChordTemplateExtensions
{
    /// <summary>
    ///     Determines if this chord template is scale-derived
    /// </summary>
    public static bool IsScaleDerived(this ChordTemplate template)
    {
        return template switch
        {
            ChordTemplate.TonalModal => true,
            _ => false
        };
    }

    /// <summary>
    ///     Gets the parent scale if this is a scale-derived chord
    /// </summary>
    public static ScaleMode? GetParentScale(this ChordTemplate template)
    {
        return template switch
        {
            ChordTemplate.TonalModal tonal => tonal.ParentScale,
            _ => null
        };
    }

    /// <summary>
    ///     Gets the scale degree/position if this is a scale-derived chord
    /// </summary>
    public static int? GetScaleDegree(this ChordTemplate template)
    {
        return template switch
        {
            ChordTemplate.TonalModal tonal => tonal.ScaleDegree,
            _ => null
        };
    }

    /// <summary>
    ///     Gets the harmonic function if this is a tonal modal chord
    /// </summary>
    public static string? GetHarmonicFunction(this ChordTemplate template)
    {
        return template switch
        {
            ChordTemplate.TonalModal tonal => tonal.HarmonicFunction.ToString(),
            _ => null
        };
    }

    /// <summary>
    ///     Gets a human-readable description of this chord template
    /// </summary>
    public static string GetDescription(this ChordTemplate template)
    {
        return template switch
        {
            ChordTemplate.TonalModal tonal => tonal.Description,
            ChordTemplate.Analytical analytical => analytical.Description,
            _ => "Unknown chord type"
        };
    }

    /// <summary>
    ///     Gets the construction type of this chord template
    /// </summary>
    public static string GetConstructionType(this ChordTemplate template)
    {
        return template switch
        {
            ChordTemplate.TonalModal => "Tonal Modal",
            ChordTemplate.Analytical => "Analytical",
            _ => "Unknown"
        };
    }

    /// <summary>
    ///     Gets metadata if this chord has metadata
    /// </summary>
    public static Dictionary<string, object>? GetMetadata(this ChordTemplate template)
    {
        return template switch
        {
            ChordTemplate.Analytical analytical => analytical.AnalysisData,
            _ => null
        };
    }
}