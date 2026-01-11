namespace GA.Business.Core.Chords;

using System;
using System.Collections.Generic;
using System.Linq;
using Atonal;
using Intervals;
using Intervals.Primitives;
using Tonal.Modes;

/// <summary>
///     Represents a chord template that defines the structure and properties of a chord type.
///     This discriminated union captures all possible chord construction scenarios exhaustively.
/// </summary>
public abstract record ChordTemplate
{
    private readonly Lazy<PitchClassSet> _lazyPitchClassSet;

    /// <summary>Base constructor for all chord template variants</summary>
    private ChordTemplate(ChordFormula formula)
    {
        Formula = formula ?? throw new ArgumentNullException(nameof(formula));
        _lazyPitchClassSet = new(() => CreatePitchClassSet(formula));
    }

    /// <summary>Gets the chord formula defining this chord's interval structure</summary>
    public ChordFormula Formula { get; }

    /// <summary>Gets the name of this chord from the formula</summary>
    public string Name => Formula.Name;

    /// <summary>Gets the chord quality</summary>
    public ChordQuality Quality => Formula.Quality;

    /// <summary>Gets the chord extension</summary>
    public ChordExtension Extension => Formula.Extension;

    /// <summary>Gets the stacking type</summary>
    public ChordStackingType StackingType => Formula.StackingType;

    /// <summary>Gets the number of notes in this chord</summary>
    public int NoteCount => Formula.Intervals.Count + 1; // +1 for root

    /// <summary>Gets the intervals that define this chord</summary>
    public IReadOnlyCollection<ChordFormulaInterval> Intervals => Formula.Intervals;

    /// <summary>Gets the characteristic intervals (defining chord quality)</summary>
    public IReadOnlyCollection<ChordFormulaInterval> CharacteristicIntervals =>
        Formula.Intervals.Where(i => i.IsEssential).ToList().AsReadOnly();

    /// <summary>Gets the pitch class set derived from the chord formula</summary>
    public PitchClassSet PitchClassSet => _lazyPitchClassSet.Value;


    /// <summary>
    ///     Gets the chord symbol suffix (e.g., "maj7", "m", "dim")
    /// </summary>
    public string GetSymbolSuffix()
    {
        return Formula.GetSymbolSuffix();
    }

    /// <summary>
    ///     Creates a pitch class set from a chord formula
    /// </summary>
    private static PitchClassSet CreatePitchClassSet(ChordFormula formula)
    {
        var pitchClasses = new List<PitchClass> { PitchClass.FromValue(0) }; // Root at C

        foreach (var interval in formula.Intervals)
        {
            var pitchClass = PitchClass.FromSemitones(interval.Interval.Semitones);
            pitchClasses.Add(pitchClass);
        }

        return new(pitchClasses);
    }

    /// <summary>
    ///     Analyzes a pitch class set to create a chord formula
    /// </summary>
    internal static ChordFormula AnalyzePitchClassSet(PitchClassSet pitchClassSet, string name)
    {
        var pitchClasses = pitchClassSet.ToList();
        if (pitchClasses.Count == 0)
        {
            throw new ArgumentException("Pitch class set cannot be empty", nameof(pitchClassSet));
        }

        // Assume first pitch class is root (legacy behavior)
        var root = pitchClasses[0];
        return AnalyzePitchClassSet(pitchClassSet, root, name);
    }
    
    /// <summary>
    ///     Analyzes a pitch class set to create a chord formula with explicit root
    /// </summary>
    internal static ChordFormula AnalyzePitchClassSet(PitchClassSet pitchClassSet, PitchClass root, string name)
    {
        var pitchClasses = pitchClassSet.ToList();
        if (pitchClasses.Count == 0)
        {
            throw new ArgumentException("Pitch class set cannot be empty", nameof(pitchClassSet));
        }

        var intervals = new List<ChordFormulaInterval>();
        var semitonesList = pitchClasses.Select(pc => (pc.Value - root.Value + 12) % 12).Where(s => s != 0).ToList();

        foreach (var semitones in semitonesList)
        {
            var interval = new Interval.Chromatic(Semitones.FromValue(semitones));
            var function = DetermineChordFunction(semitones);

            // SPECIAL CASE: Hendrix Chord / Split Thirds
            // If we have both 3 and 4 semitones, 4 is the Third and 3 is the Sharp Ninth
            if (semitones == 3 && semitonesList.Contains(4))
            {
                function = ChordFunction.Ninth;
            }

            intervals.Add(new(interval, function));
        }

        return new(name, intervals);
    }

    private static ChordFunction DetermineChordFunction(int semitones)
    {
        return semitones switch
        {
            2 or 14 => ChordFunction.Ninth,
            3 or 4 => ChordFunction.Third,
            5 or 17 => ChordFunction.Eleventh,
            7 => ChordFunction.Fifth,
            9 or 21 => ChordFunction.Thirteenth,
            10 or 11 => ChordFunction.Seventh,
            _ => ChordFunction.Root
        };
    }

    public override string ToString()
    {
        return $"{Name} ({GetSymbolSuffix()})";
    }

    /// <summary>
    ///     A chord derived from a traditional tonal scale mode (major/minor scale families)
    /// </summary>
    public sealed record TonalModal(
        ChordFormula Formula,
        ScaleMode ParentScale,
        int ScaleDegree) : ChordTemplate(Formula)
    {
        /// <summary>Gets the harmonic function of this chord within its parent scale</summary>
        public string HarmonicFunction => ScaleDegree switch
        {
            1 => "Tonic",
            2 => "Supertonic",
            3 => "Mediant",
            4 => "Subdominant",
            5 => "Dominant",
            6 => "Submediant",
            7 => "Leading Tone",
            _ => $"Degree {ScaleDegree}"
        };

        /// <summary>Gets a descriptive name for this tonal modal chord</summary>
        public string Description =>
            $"{HarmonicFunction} ({ScaleDegree}) in {ParentScale.Name} - {Extension} {StackingType}";
    }


    /// <summary>
    ///     A chord derived from pitch class set analysis or set theory
    /// </summary>
    public sealed record Analytical(
        ChordFormula Formula,
        string AnalysisMethod,
        PitchClassSet? SourcePitchClassSet = null,
        Dictionary<string, object>? AnalysisData = null) : ChordTemplate(Formula)
    {
        /// <summary>Gets a descriptive name for this analytical chord</summary>
        public string Description => $"Analytical chord via {AnalysisMethod} - {Extension} {StackingType}";

        /// <summary>Creates a chord from pitch class set analysis</summary>
        public static Analytical FromPitchClassSet(PitchClassSet pitchClassSet, string name)
        {
            var formula = AnalyzePitchClassSet(pitchClassSet, name);
            return new(formula, "Pitch Class Set Analysis", pitchClassSet,
                new()
                    { ["SourceName"] = name });
        }
        
        /// <summary>Creates a chord from pitch class set analysis with explicit root</summary>
        public static Analytical FromPitchClassSet(PitchClassSet pitchClassSet, PitchClass root, string name)
        {
            var formula = AnalyzePitchClassSet(pitchClassSet, root, name);
            return new(formula, "Pitch Class Set Analysis", pitchClassSet,
                new()
                    { ["SourceName"] = name, ["Root"] = root });
        }

        /// <summary>Creates a chord from set theory analysis</summary>
        public static Analytical FromSetTheory(ChordFormula formula, string setClass,
            Dictionary<string, object>? analysisData = null)
        {
            return new(formula, "Set Theory Analysis", null,
                analysisData ?? new Dictionary<string, object> { ["SetClass"] = setClass });
        }
    }
}

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
            ChordTemplate.TonalModal tonal => tonal.HarmonicFunction,
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
