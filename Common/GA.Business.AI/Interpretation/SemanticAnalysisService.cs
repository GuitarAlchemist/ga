namespace GA.Business.AI.Interpretation;

using GA.Business.Core.Atonal;
using GA.Business.Core.Atonal.Primitives;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Interpretive music analysis service providing natural language-friendly insights.
/// Designed to power conversational AI and semantic descriptions of music theory concepts.
/// </summary>
[PublicAPI]
public static class SemanticAnalysisService
{
    #region Neo-Riemannian Operations

    /// <summary>
    /// Performs a Neo-Riemannian L (Leading-tone exchange) operation on a triad.
    /// Swaps the root of a major triad with the note a semitone below, or vice versa.
    /// </summary>
    public static NeoRiemannianResult? ApplyLeadingtone(PitchClassSet triad, PitchClass root)
    {
        if (triad.Count != 3) return null;

        var pcs = triad.OrderBy(pc => pc.Value).ToList();
        var isMajor = IsMajorTriad(pcs, root);
        var isMinor = IsMinorTriad(pcs, root);

        if (!isMajor && !isMinor) return null;

        string sourceTriad, targetTriad;
        PitchClass newRoot;

        if (isMajor)
        {
            newRoot = PitchClass.FromValue((root.Value + 4) % 12);
            sourceTriad = $"{root.ToSharpNote()} major";
            targetTriad = $"{newRoot.ToSharpNote()} minor";
        }
        else
        {
            newRoot = PitchClass.FromValue((root.Value + 8) % 12);
            sourceTriad = $"{root.ToSharpNote()} minor";
            targetTriad = $"{newRoot.ToSharpNote()} major";
        }

        return new NeoRiemannianResult(
            "L (Leading-tone)",
            sourceTriad,
            targetTriad,
            2,
            $"The L operation transforms {sourceTriad} to {targetTriad} by moving one note by a semitone, keeping 2 common tones.");
    }

    /// <summary>
    /// Performs a Neo-Riemannian P (Parallel) operation on a triad.
    /// </summary>
    public static NeoRiemannianResult? ApplyParallel(PitchClassSet triad, PitchClass root)
    {
        if (triad.Count != 3) return null;

        var pcs = triad.OrderBy(pc => pc.Value).ToList();
        var isMajor = IsMajorTriad(pcs, root);
        var isMinor = IsMinorTriad(pcs, root);

        if (!isMajor && !isMinor) return null;

        string sourceTriad, targetTriad;

        if (isMajor)
        {
            sourceTriad = $"{root.ToSharpNote()} major";
            targetTriad = $"{root.ToSharpNote()} minor";
        }
        else
        {
            sourceTriad = $"{root.ToSharpNote()} minor";
            targetTriad = $"{root.ToSharpNote()} major";
        }

        return new NeoRiemannianResult(
            "P (Parallel)",
            sourceTriad,
            targetTriad,
            2,
            $"The P operation transforms {sourceTriad} to its parallel {targetTriad} by moving the 3rd, keeping root and 5th.");
    }

    /// <summary>
    /// Performs a Neo-Riemannian R (Relative) operation on a triad.
    /// </summary>
    public static NeoRiemannianResult? ApplyRelative(PitchClassSet triad, PitchClass root)
    {
        if (triad.Count != 3) return null;

        var pcs = triad.OrderBy(pc => pc.Value).ToList();
        var isMajor = IsMajorTriad(pcs, root);
        var isMinor = IsMinorTriad(pcs, root);

        if (!isMajor && !isMinor) return null;

        string sourceTriad, targetTriad;
        PitchClass newRoot;

        if (isMajor)
        {
            newRoot = PitchClass.FromValue((root.Value + 9) % 12);
            sourceTriad = $"{root.ToSharpNote()} major";
            targetTriad = $"{newRoot.ToSharpNote()} minor";
        }
        else
        {
            newRoot = PitchClass.FromValue((root.Value + 3) % 12);
            sourceTriad = $"{root.ToSharpNote()} minor";
            targetTriad = $"{newRoot.ToSharpNote()} major";
        }

        return new NeoRiemannianResult(
            "R (Relative)",
            sourceTriad,
            targetTriad,
            2,
            $"The R operation transforms {sourceTriad} to its relative {targetTriad}. These share 2 common tones and are in the same key.");
    }

    /// <summary>
    /// Analyzes the relationship between two triads.
    /// </summary>
    public static string ExplainTriadRelationship(PitchClassSet source, PitchClass sourceRoot, 
                                                   PitchClassSet target, PitchClass targetRoot)
    {
        if (source.Count != 3 || target.Count != 3) 
            return "Neo-Riemannian operations only apply to triads (3-note chords).";

        _ = ApplyLeadingtone(source, sourceRoot);
        _ = ApplyParallel(source, sourceRoot);
        _ = ApplyRelative(source, sourceRoot);

        return $"To move between triads, you can use L (Leading-tone), P (Parallel), or R (Relative) operations. " +
               $"Each operation keeps 2 common tones while moving only 1 note by 1-2 semitones.";
    }

    #endregion

    #region Interval Content Analysis

    /// <summary>
    /// Provides rich interval content analysis with descriptive interpretations.
    /// </summary>
    public static IntervalContentAnalysis AnalyzeIntervalContent(PitchClassSet set)
    {
        var icv = set.IntervalClassVector;
        
        var semitones = icv[IntervalClass.Hemitone];
        var tritones = icv[IntervalClass.Tritone];
        var fifths = icv[IntervalClass.FromValue(5)];
        var wholeTones = icv[IntervalClass.Tone];
        var minorThirds = icv[IntervalClass.FromValue(3)];
        var majorThirds = icv[IntervalClass.FromValue(4)];

        var hasQuartal = fifths >= 2;

        var dissonanceScore = semitones * 3 + tritones * 2 + wholeTones;
        var dissonanceLevel = dissonanceScore switch
        {
            0 => "Low",
            <= 3 => "Medium",
            <= 6 => "High",
            _ => "Extreme"
        };

        var characteristics = new List<string>();
        if (tritones > 0) characteristics.Add($"{tritones} tritone(s) for tension");
        if (hasQuartal) characteristics.Add("quartal/quintal harmony");
        if (semitones >= 2) characteristics.Add("cluster-like density");
        if (minorThirds + majorThirds >= 2 && semitones == 0) characteristics.Add("tertian (stacked thirds)");
        if (fifths >= 3) characteristics.Add("open, hollow sound");

        var description = characteristics.Count > 0
            ? $"This set has {string.Join(", ", characteristics)}."
            : "This set has a balanced intervallic profile.";

        return new IntervalContentAnalysis(
            tritones,
            semitones,
            fifths,
            hasQuartal,
            dissonanceLevel,
            description);
    }

    #endregion

    #region Set Complement

    /// <summary>
    /// Gets the complement of a pitch class set (remaining chromatic notes).
    /// </summary>
    public static SetComplementAnalysis GetComplement(PitchClassSet set)
    {
        var allPitchClasses = Enumerable.Range(0, 12).Select(PitchClass.FromValue).ToHashSet();
        var original = set.ToHashSet();
        var complement = allPitchClasses.Except(original).ToList();

        var complementSet = new PitchClassSet(complement);
        var noteNames = complement.Select(pc => pc.ToSharpNote().ToString()).ToList();

        return new SetComplementAnalysis(
            set,
            complementSet,
            complement.Count,
            noteNames);
    }

    #endregion

    #region Modal Interchange

    /// <summary>
    /// Analyzes modal interchange possibilities between parallel modes.
    /// </summary>
    public static ModalInterchangeAnalysis? AnalyzeModalInterchange(
        PitchClassSet sourceMode, 
        PitchClassSet targetMode,
        PitchClass root)
    {
        if (sourceMode.Count != 7 || targetMode.Count != 7)
            return null;

        var sourceNotes = sourceMode.OrderBy(pc => (pc.Value - root.Value + 12) % 12).ToList();
        var targetNotes = targetMode.OrderBy(pc => (pc.Value - root.Value + 12) % 12).ToList();

        var borrowedDegrees = new List<string>();
        var degreeNames = new[] { "1", "2", "3", "4", "5", "6", "7" };

        for (var i = 0; i < 7; i++)
        {
            var sourceDegree = (sourceNotes[i].Value - root.Value + 12) % 12;
            var targetDegree = (targetNotes[i].Value - root.Value + 12) % 12;

            if (sourceDegree != targetDegree)
            {
                var diff = (targetDegree - sourceDegree + 12) % 12;
                var modifier = diff <= 6 ? "♯" : "♭";
                borrowedDegrees.Add($"{modifier}{degreeNames[i]}");
            }
        }

        var suggestedChords = new List<string>();
        if (borrowedDegrees.Any(d => d.Contains("3"))) suggestedChords.Add("♭III");
        if (borrowedDegrees.Any(d => d.Contains("7"))) suggestedChords.Add("♭VII");
        if (borrowedDegrees.Any(d => d.Contains("6"))) suggestedChords.Add("iv");

        var explanation = borrowedDegrees.Count > 0
            ? $"You can borrow {string.Join(", ", suggestedChords)} from the parallel mode. " +
              $"These chords use {string.Join(", ", borrowedDegrees)} which creates a distinctive color."
            : "These modes share all scale degrees - no borrowing is needed.";

        return new ModalInterchangeAnalysis(
            $"{root.ToSharpNote()} source",
            $"{root.ToSharpNote()} target",
            borrowedDegrees,
            suggestedChords,
            explanation);
    }

    #endregion

    #region Private Helpers

    private static bool IsMajorTriad(List<PitchClass> pcs, PitchClass root)
    {
        var intervals = pcs.Select(pc => (pc.Value - root.Value + 12) % 12).OrderBy(i => i).ToArray();
        return intervals.SequenceEqual(new[] { 0, 4, 7 });
    }

    private static bool IsMinorTriad(List<PitchClass> pcs, PitchClass root)
    {
        var intervals = pcs.Select(pc => (pc.Value - root.Value + 12) % 12).OrderBy(i => i).ToArray();
        return intervals.SequenceEqual(new[] { 0, 3, 7 });
    }

    #endregion
}
