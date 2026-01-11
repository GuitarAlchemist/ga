namespace GA.Business.Core.Chords.Analysis.Atonal;

using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Atonal;

/// <summary>
///     Service for analyzing chords using atonal theory when traditional tonal analysis fails
/// </summary>
public static class AtonalChordAnalysisService
{
    /// <summary>
    ///     Determines if a chord requires atonal analysis
    /// </summary>
    public static bool RequiresAtonalAnalysis(ChordTemplate template)
    {
        // Check for characteristics that suggest atonal harmony
        var intervals = template.Intervals.Select(i => i.Interval.Semitones.Value).ToList();

        // More than 6 different pitch classes suggests complex harmony
        if (intervals.Count > 6)
        {
            return true;
        }

        // Check for clusters (many semitones)
        var semitoneCount = intervals.Count(i => i == 1);
        if (semitoneCount >= 2)
        {
            return true;
        }

        // Check for symmetrical intervals
        if (HasSymmetricalStructure(intervals))
        {
            return true;
        }

        // Check for non-tertian stacking with complex intervals
        if (template.StackingType != ChordStackingType.Tertian && intervals.Count > 4)
        {
            return true;
        }

        // Check for many altered tones
        var alterationCount = intervals.Count(i => i is 1 or 3 or 6 or 8);
        if (alterationCount >= 3)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Performs atonal analysis of a chord template
    /// </summary>
    public static AtonalAnalysis AnalyzeAtonally(ChordTemplate template, PitchClass root)
    {
        // Create pitch class set from chord intervals
        var pitchClasses = new List<PitchClass> { root };
        foreach (var interval in template.Intervals)
        {
            var pc = PitchClass.FromValue((root.Value + interval.Interval.Semitones.Value) % 12);
            if (!pitchClasses.Contains(pc))
            {
                pitchClasses.Add(pc);
            }
        }

        var pitchClassSet = new PitchClassSet(pitchClasses);
        var setClass = new SetClass(pitchClassSet);

        var primeForm = GetPrimeFormString(setClass.PrimeForm);
        var forteNumber = GenerateForteNumber(setClass);
        var intervalClassVector = setClass.IntervalClassVector;
        var suggestedName = GenerateAtonalName(root, setClass, pitchClassSet);
        var theoreticalDescription = GenerateTheoreticalDescription(setClass, pitchClassSet);
        var isSymmetrical = IsSymmetricalSet(pitchClassSet);
        var isModal = setClass.IsModal;
        var alternateNames = GenerateAlternateAtonalNames(root, setClass, pitchClassSet);

        return new(
            pitchClassSet, setClass, primeForm, forteNumber, intervalClassVector,
            suggestedName, theoreticalDescription, isSymmetrical, isModal, alternateNames);
    }

    /// <summary>
    ///     Generates a chord name using atonal analysis
    /// </summary>
    public static string GenerateAtonalChordName(ChordTemplate template, PitchClass root)
    {
        var analysis = AnalyzeAtonally(template, root);
        return analysis.SuggestedName;
    }

    /// <summary>
    ///     Gets comprehensive atonal information for a chord
    /// </summary>
    public static string GetAtonalDescription(ChordTemplate template, PitchClass root)
    {
        var analysis = AnalyzeAtonally(template, root);

        return $"{analysis.SuggestedName} | " +
               $"Prime Form: {analysis.PrimeForm} | " +
               $"Forte: {analysis.ForteNumber} | " +
               $"ICV: {analysis.IntervalClassVector} | " +
               $"{analysis.TheoreticalDescription}";
    }

    /// <summary>
    ///     Checks if intervals have symmetrical structure
    /// </summary>
    private static bool HasSymmetricalStructure(IList<int> intervals)
    {
        // Check for whole tone patterns (all intervals are 2 semitones apart)
        var wholeTonePattern = intervals.All(i => i % 2 == 0);

        // Check for diminished patterns (intervals in groups of 3)
        var diminishedPattern = intervals.All(i => i % 3 == 0);

        // Check for augmented patterns (intervals in groups of 4)
        var augmentedPattern = intervals.All(i => i % 4 == 0);

        return wholeTonePattern || diminishedPattern || augmentedPattern;
    }

    /// <summary>
    ///     Gets prime form as string
    /// </summary>
    private static string GetPrimeFormString(PitchClassSet primeForm)
    {
        return $"({string.Join(",", primeForm.OrderBy(pc => pc.Value))})";
    }

    /// <summary>
    ///     Generates Forte number notation
    /// </summary>
    private static string GenerateForteNumber(SetClass setClass)
    {
        if (ForteCatalog.TryGetForteNumber(setClass.PrimeForm, out var forte))
        {
            return forte.ToString();
        }

        // Fallback for failed identification
        var cardinality = setClass.Cardinality.Value;
        var icvId = setClass.IntervalClassVector.Id.Value;
        return $"{cardinality}-{icvId % 100}";
    }

    /// <summary>
    ///     Generates atonal chord name
    /// </summary>
    private static string GenerateAtonalName(PitchClass root, SetClass setClass, PitchClassSet pitchClassSet)
    {
        var rootName = GetNoteName(root);
        var cardinality = setClass.Cardinality.Value;

        // Check for common atonal chord types
        if (setClass.IsModal && setClass.ModalFamily != null)
        {
            return $"{rootName} {setClass.ModalFamily}";
        }

        // Check for symmetrical structures
        if (IsSymmetricalSet(pitchClassSet))
        {
            var symmetryType = GetSymmetryType(pitchClassSet);
            return $"{rootName} {symmetryType}";
        }

        // Check for cluster chords
        if (IsClusterChord(pitchClassSet))
        {
            return $"{rootName} cluster({cardinality})";
        }

        // Check for quartal/quintal structures
        if (IsQuartalQuintalStructure(pitchClassSet))
        {
            return $"{rootName} quartal/quintal";
        }

        // Default to set class notation
        return $"{rootName} [{cardinality}-{setClass.IntervalClassVector.Id}]";
    }

    /// <summary>
    ///     Generates theoretical description
    /// </summary>
    private static string GenerateTheoreticalDescription(SetClass setClass, PitchClassSet pitchClassSet)
    {
        var descriptions = new List<string>
        {
            $"Cardinality: {setClass.Cardinality.Value}",
            $"Interval Class Vector: {setClass.IntervalClassVector}"
        };

        if (setClass.IsModal)
        {
            descriptions.Add($"Modal family: {setClass.ModalFamily}");
        }

        if (IsSymmetricalSet(pitchClassSet))
        {
            descriptions.Add("Symmetrical structure");
        }

        if (IsClusterChord(pitchClassSet))
        {
            descriptions.Add("Cluster chord");
        }

        var consonanceLevel = AnalyzeConsonance(setClass.IntervalClassVector);
        descriptions.Add($"Consonance: {consonanceLevel}");

        return string.Join("; ", descriptions);
    }

    /// <summary>
    ///     Checks if pitch class set is symmetrical
    /// </summary>
    private static bool IsSymmetricalSet(PitchClassSet pitchClassSet)
    {
        var intervals = pitchClassSet.OrderBy(pc => pc.Value).ToList();

        // Check for equal divisions of the octave
        if (intervals.Count <= 1)
        {
            return false;
        }

        var firstInterval = (intervals[1].Value - intervals[0].Value + 12) % 12;
        return intervals.Zip(intervals.Skip(1), (a, b) => (b.Value - a.Value + 12) % 12)
            .All(interval => interval == firstInterval);
    }

    /// <summary>
    ///     Gets symmetry type description
    /// </summary>
    private static string GetSymmetryType(PitchClassSet pitchClassSet)
    {
        var count = pitchClassSet.Count;

        return count switch
        {
            2 => "tritone",
            3 => "augmented triad",
            4 => "diminished 7th",
            6 => "whole tone",
            _ => "symmetrical"
        };
    }

    /// <summary>
    ///     Checks if this is a cluster chord
    /// </summary>
    private static bool IsClusterChord(PitchClassSet pitchClassSet)
    {
        var sortedPcs = pitchClassSet.OrderBy(pc => pc.Value).ToList();
        if (sortedPcs.Count < 3)
        {
            return false;
        }

        // Check for consecutive semitones
        var consecutiveCount = 0;
        for (var i = 1; i < sortedPcs.Count; i++)
        {
            if (sortedPcs[i].Value - sortedPcs[i - 1].Value == 1)
            {
                consecutiveCount++;
            }
        }

        return consecutiveCount >= 2;
    }

    /// <summary>
    ///     Checks if this has quartal/quintal structure
    /// </summary>
    private static bool IsQuartalQuintalStructure(PitchClassSet pitchClassSet)
    {
        var sortedPcs = pitchClassSet.OrderBy(pc => pc.Value).ToList();
        if (sortedPcs.Count < 3)
        {
            return false;
        }

        var fourthFifthCount = 0;
        for (var i = 1; i < sortedPcs.Count; i++)
        {
            var interval = (sortedPcs[i].Value - sortedPcs[i - 1].Value + 12) % 12;
            if (interval is 5 or 7) // Perfect 4th or 5th
            {
                fourthFifthCount++;
            }
        }

        return fourthFifthCount >= sortedPcs.Count / 2;
    }

    /// <summary>
    ///     Analyzes consonance level
    /// </summary>
    private static string AnalyzeConsonance(IntervalClassVector icv)
    {
        // Simplified consonance analysis based on interval class vector
        var icvString = icv.ToString();
        var dissonantIntervals = icvString.Take(4).Sum(c => c - '0'); // IC 1,2,6 are most dissonant

        return dissonantIntervals switch
        {
            0 => "Highly consonant",
            <= 2 => "Moderately consonant",
            <= 4 => "Moderately dissonant",
            _ => "Highly dissonant"
        };
    }

    /// <summary>
    ///     Generates alternate atonal names
    /// </summary>
    private static IReadOnlyList<string> GenerateAlternateAtonalNames(PitchClass root, SetClass setClass,
        PitchClassSet pitchClassSet)
    {
        var alternates = new List<string>();
        var rootName = GetNoteName(root);

        // Add prime form notation
        alternates.Add($"{rootName} {GetPrimeFormString(setClass.PrimeForm)}");

        // Add Forte number notation
        alternates.Add($"{rootName} {GenerateForteNumber(setClass)}");

        // Add interval class vector notation
        alternates.Add($"{rootName} ICV[{setClass.IntervalClassVector}]");

        // Add cardinality-based name
        alternates.Add($"{rootName} {setClass.Cardinality.Value}-note chord");

        return alternates.AsReadOnly();
    }

    /// <summary>
    ///     Gets the note name for a pitch class
    /// </summary>
    private static string GetNoteName(PitchClass pitchClass)
    {
        return pitchClass.Value switch
        {
            0 => "C", 1 => "C#", 2 => "D", 3 => "D#", 4 => "E", 5 => "F",
            6 => "F#", 7 => "G", 8 => "G#", 9 => "A", 10 => "A#", 11 => "B",
            _ => "?"
        };
    }

    /// <summary>
    ///     Atonal analysis result for a chord
    /// </summary>
    public record AtonalAnalysis(
        PitchClassSet PitchClassSet,
        SetClass SetClass,
        string PrimeForm,
        string ForteNumber,
        IntervalClassVector IntervalClassVector,
        string SuggestedName,
        string TheoreticalDescription,
        bool IsSymmetrical,
        bool IsModal,
        IReadOnlyList<string> AlternateNames);
}
