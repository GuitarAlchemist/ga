namespace GA.Business.Core.Tonal;

using System;

/// <summary>
/// Unified logic for resolving and parsing harmonic functions.
/// </summary>
public static class HarmonicFunctionAnalyzer
{
    /// <summary>
    /// Resolves harmonic function from a 1-based scale degree.
    /// </summary>
    public static HarmonicFunction FromScaleDegree(int degree)
    {
        return degree switch
        {
            1 => HarmonicFunction.Tonic,
            2 => HarmonicFunction.Supertonic,
            3 => HarmonicFunction.Mediant,
            4 => HarmonicFunction.Subdominant,
            5 => HarmonicFunction.Dominant,
            6 => HarmonicFunction.Submediant,
            7 => HarmonicFunction.LeadingTone,
            _ => HarmonicFunction.Unknown
        };
    }

    /// <summary>
    /// Parses a string representation of harmonic function.
    /// Supports both Name ("Tonic") and Roman ("I").
    /// </summary>
    public static HarmonicFunction Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return HarmonicFunction.Unknown;

        var v = value.Trim().ToLowerInvariant();
        
        // Match by Name
        if (v.Contains("tonic")) return HarmonicFunction.Tonic;
        if (v.Contains("supertonic")) return HarmonicFunction.Supertonic;
        if (v.Contains("mediant")) return HarmonicFunction.Mediant;
        if (v.Contains("subdominant")) return HarmonicFunction.Subdominant;
        if (v.Contains("dominant")) return HarmonicFunction.Dominant;
        if (v.Contains("submediant")) return HarmonicFunction.Submediant;
        if (v.Contains("leading")) return HarmonicFunction.LeadingTone;

        // Match by Roman (Basic)
        return v switch
        {
            "i" => HarmonicFunction.Tonic,
            "ii" => HarmonicFunction.Supertonic,
            "iii" => HarmonicFunction.Mediant,
            "iv" => HarmonicFunction.Subdominant,
            "v" => HarmonicFunction.Dominant,
            "vi" => HarmonicFunction.Submediant,
            "vii" => HarmonicFunction.LeadingTone,
            _ => HarmonicFunction.Unknown
        };
    }

    /// <summary>
    /// Groups granular functions into primary functional categories (Tonic, Subdominant, Dominant).
    /// Used for RAG retrieval and high-level categorization.
    /// </summary>
    public static HarmonicFunctionCategory ToPrimaryCategory(HarmonicFunction function)
    {
        return function switch
        {
            HarmonicFunction.Tonic or HarmonicFunction.Submediant or HarmonicFunction.Mediant => HarmonicFunctionCategory.Tonic,
            HarmonicFunction.Subdominant or HarmonicFunction.Supertonic => HarmonicFunctionCategory.Subdominant,
            HarmonicFunction.Dominant or HarmonicFunction.LeadingTone => HarmonicFunctionCategory.Dominant,
            _ => HarmonicFunctionCategory.Ambiguous
        };
    }
}
