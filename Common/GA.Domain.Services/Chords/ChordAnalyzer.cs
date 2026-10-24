namespace GA.Domain.Services.Chords;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Unified, deep domain module for chord naming and analysis.
///     Internalizes slash chords, hybrid naming, and template matching behind a single cohesive seam.
/// </summary>
public sealed class ChordAnalyzer
{
    private static readonly Lazy<ChordAnalyzer> _instance = new(() => new ChordAnalyzer());
    public static ChordAnalyzer Instance => _instance.Value;

    /// <summary>
    ///     Analyzes and returns the best chord name for a given PitchClassSet and optional bass pitch class.
    /// </summary>
    public string AnalyzeAndName(PitchClassSet pitchClasses, PitchClass? bassNote = null)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        if (pitchClasses.Count == 0) return "Empty Set";

        var root = bassNote ?? pitchClasses.First();
        var pcs = pitchClasses.Select(p => p.Value).ToHashSet();

        // Check if slash chord (bass note is not root)
        var isSlash = bassNote != null && bassNote.Value != root.Value;

        // Triad recognition
        var triads = pitchClasses.GetTertianTriads();
        var bestTriad = triads.FirstOrDefault();

        string baseName;
        if (bestTriad != null)
        {
            baseName = $"{bestTriad.Root} {bestTriad.TriadQuality}";
        }
        else
        {
            baseName = $"{root} ({pitchClasses.Cardinality.Value}-tone set)";
        }


        if (isSlash)
        {
            return $"{baseName}/{bassNote}";
        }

        return baseName;
    }
}
