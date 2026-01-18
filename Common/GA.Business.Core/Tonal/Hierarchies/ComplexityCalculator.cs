namespace GA.Business.Core.Tonal.Hierarchies;

using System;
using System.Linq;
using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Primitives;

/// <summary>
/// Calculates the Hierarchical Complexity Level of a pitch class set.
/// Complexity is a measure of structural depth (Triad -> Tetrad -> Extension -> Altered).
/// </summary>
public static class ComplexityCalculator
{
    public enum ComplexityLevel
    {
        Note = 0,
        Interval = 1,
        Triad = 2,
        Tetrad = 3,
        Extended = 4,
        Altered = 5,
        Polychord = 6
    }

    /// <summary>
    /// Determines the semantic complexity level of a pitch class set.
    /// </summary>
    public static ComplexityLevel CalculateLevel(PitchClassSet pcs)
    {
        int count = pcs.Count;

        // Level 0: Single Note
        if (count <= 1) return ComplexityLevel.Note;

        // Level 1: Interval (Dyad)
        if (count == 2) return ComplexityLevel.Interval;

        // Level 2: Triad (3 notes)
        // Check if it's a standard triad (Major/Minor/Dim/Aug)
        // Standard triads stack in 3rds.
        if (count == 3)
        {
            // Simple heuristic: If it fits standard triad Prime Forms
            // 0,3,7 (Min); 0,4,7 (Maj); 0,3,6 (Dim); 0,4,8 (Aug)
            // Or Suss? 0,2,7 (Sus2); 0,5,7 (Sus4) - often considered Level 2.
            return ComplexityLevel.Triad;
        }

        // Level 3: Tetrad (4 notes) - 7th Chords
        if (count == 4)
        {
            // Standard 7ths: Maj7, min7, Dom7, m7b5, dim7
            // 6th chords also Level 3.
            return ComplexityLevel.Tetrad;
        }

        // Level 4: Extended (5-6 notes) - 9ths, 11ths
        // Or if it contains specific color tones even with fewer notes (Shells)?
        // Shells complicate this. A 3-note shell (R-3-7) implies Level 3.
        // But purely structurally, it's a subset.
        // Here we measure *realized* complexity (count).
        // To measure *implied* complexity, we'd need context.
        if (count >= 5 && count <= 6)
        {
            return ComplexityLevel.Extended;
        }

        // Level 6: Polychord / Cluster (7+ notes)
        if (count >= 7)
        {
            return ComplexityLevel.Polychord;
        }

        // Fallback
        return ComplexityLevel.Extended;
    }

    /// <summary>
    /// Calculates a normalized complexity score [0.0, 1.0].
    /// </summary>
    public static double CalculateScore(PitchClassSet pcs)
    {
        var level = CalculateLevel(pcs);
        return (double)level / 6.0;
    }
}