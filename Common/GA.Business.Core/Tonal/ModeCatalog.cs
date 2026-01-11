namespace GA.Business.Core.Tonal;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Atonal;
using Atonal.Primitives;
using GA.Business.Core.Notes.Primitives;

/// <summary>
///     Catalog of musical modes and scale families.
///     Provides a generic way to access mode definitions and metadata.
/// </summary>
public static class ModeCatalog
{
    private static readonly Dictionary<string, ModeFamilyMetadata> _metadata = new(StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, ModeFamilyMetadata> Metadata => _metadata;

    static ModeCatalog()
    {
        InitializeFamilies();
    }

    /// <summary>
    /// Try to get family metadata by Interval Class Vector string
    /// </summary>
    public static bool TryGetFamily(string icv, out ModeFamilyMetadata metadata)
    {
        return _metadata.TryGetValue(icv, out metadata!);
    }

    private static void InitializeFamilies()
    {
        // Major Scale Family
        // Ionian, Dorian, Phrygian, Lydian, Mixolydian, Aeolian, Locrian
        RegisterFamily(
            "Major Scale Family",
            [0, 2, 4, 5, 7, 9, 11],
            [
                "Ionian (Major)",
                "Dorian",
                "Phrygian",
                "Lydian",
                "Mixolydian",
                "Aeolian (Natural Minor)",
                "Locrian"
            ]);

        // Harmonic Minor Family
        RegisterFamily(
            "Harmonic Minor Family",
            [0, 2, 3, 5, 7, 8, 11],
            [
                "Harmonic Minor",
                "Locrian ♮6",
                "Ionian ♯5 (Ionian Augmented)",
                "Dorian ♯4 (Romanian)",
                "Phrygian Dominant",
                "Lydian ♯2",
                "Ultralocrian (Superlocrian ♭♭7)"
            ]);

        // Melodic Minor Family
        RegisterFamily(
            "Melodic Minor Family",
            [0, 2, 3, 5, 7, 9, 11],
            [
                "Melodic Minor (Jazz Minor)",
                "Dorian ♭2 (Phrygian ♮6)",
                "Lydian Augmented (Lydian ♯5)",
                "Lydian Dominant (Overtone)",
                "Mixolydian ♭6 (Aeolian Dominant)",
                "Locrian ♮2 (Half Diminished)",
                "Altered (Super Locrian)"
            ]);

        // Harmonic Major Family
        RegisterFamily(
            "Harmonic Major Family",
            [0, 2, 4, 5, 7, 8, 11],
            [
                "Harmonic Major",
                "Dorian ♭5",
                "Phrygian ♭4",
                "Lydian ♭3",
                "Mixolydian ♭2",
                "Lydian Augmented ♯2",
                "Locrian ♭♭7"
            ]);

        // Double Harmonic Family
        RegisterFamily(
            "Double Harmonic Family",
            [0, 1, 4, 5, 7, 8, 11],
            [
                "Double Harmonic (Byzantine)",
                "Lydian ♯2 ♯6",
                "Ultraphrygian",
                "Hungarian Minor",
                "Oriental",
                "Ionian Augmented ♯2",
                "Locrian ♭♭3 ♭♭7"
            ]);

        // Neapolitan Minor
        RegisterFamily(
            "Neapolitan Minor Family",
            [0, 1, 3, 5, 7, 8, 11],
             [
                "Neapolitan Minor",
                "Lydian ♯6",
                "Mixolydian Augmented",
                "Aeolian ♯4 (Lydian Diminished)",
                "Locrian ♮3",
                "Ionian ♯2",
                "Ultralocrian ♭♭3"
            ]);

        // Neapolitan Major
        RegisterFamily(
            "Neapolitan Major Family",
            [0, 1, 3, 5, 7, 9, 11],
            [
                "Neapolitan Major",
                "Lydian Augmented ♯6",
                "Lydian Dominant ♯5",
                "Lydian Minor (Lydian ♭3 ♭7)",
                "Major Locrian",
                "Altered Dominant ♮2 (Locrian ♮2 ♮7)", // Debateable names
                "Altered ♭♭3"
            ]);

        InitializePentatonicFamilies();
        InitializeSymmetricalFamilies();
        InitializeBebopFamilies();
        InitializeExoticFamilies();
    }

    private static void InitializeBebopFamilies()
    {
        // Dominant Bebop (Mixolydian + Major 7 passing tone)
        // 8 notes: 0, 2, 4, 5, 7, 9, 10, 11
        RegisterFamily(
            "Dominant Bebop Family",
            [0, 2, 4, 5, 7, 9, 10, 11],
            [
                "Dominant Bebop",
                "Dominant Bebop Mode 2",
                "Dominant Bebop Mode 3",
                "Dominant Bebop Mode 4",
                "Dominant Bebop Mode 5",
                "Dominant Bebop Mode 6",
                "Dominant Bebop Mode 7",
                "Dominant Bebop Mode 8"
            ]);

        // Major Bebop (Major + #5/b6 passing tone)
        // 8 notes: 0, 2, 4, 5, 7, 8, 9, 11
        RegisterFamily(
            "Major Bebop Family",
            [0, 2, 4, 5, 7, 8, 9, 11],
            [
                "Major Bebop",
                "Major Bebop Mode 2",
                "Major Bebop Mode 3",
                "Major Bebop Mode 4",
                "Major Bebop Mode 5",
                "Major Bebop Mode 6",
                "Major Bebop Mode 7",
                "Major Bebop Mode 8"
            ]);
    }

    private static void InitializeExoticFamilies()
    {
        // Blues Scale (Hexatonic)
        // 1, b3, 4, b5, 5, b7
        // 0, 3, 5, 6, 7, 10
        RegisterFamily(
            "Blues Scale Family",
            [0, 3, 5, 6, 7, 10],
            [
                "Blues Scale",
                "Major Blues Scale (inverted)", // Starts on relative major
                "Blues Scale Mode 3",
                "Blues Scale Mode 4",
                "Blues Scale Mode 5",
                "Blues Scale Mode 6"
            ]);

        // Prometheus Family
        // 0, 2, 4, 6, 9, 10
        RegisterFamily(
            "Prometheus Family",
            [0, 2, 4, 6, 9, 10],
            [
                "Prometheus",
                "Prometheus Mode 2",
                "Prometheus Mode 3",
                "Prometheus Mode 4",
                "Prometheus Mode 5",
                "Prometheus Mode 6"
            ]);

        // Enigmatic Family
        // 0, 1, 4, 6, 8, 10, 11
        RegisterFamily(
            "Enigmatic Family",
            [0, 1, 4, 6, 8, 10, 11],
            [
                "Enigmatic",
                "Enigmatic Mode 2",
                "Enigmatic Mode 3",
                "Enigmatic Mode 4",
                "Enigmatic Mode 5",
                "Enigmatic Mode 6",
                "Enigmatic Mode 7"
            ]);

        // Hungarian Major Family
        // 0, 3, 4, 6, 7, 9, 10
        RegisterFamily(
            "Hungarian Major Family",
             [0, 3, 4, 6, 7, 9, 10],
             [
                "Hungarian Major",
                "Hungarian Major Mode 2",
                "Hungarian Major Mode 3",
                "Hungarian Major Mode 4",
                "Hungarian Major Mode 5",
                "Hungarian Major Mode 6",
                "Hungarian Major Mode 7"
             ]);
    }

    private static void InitializePentatonicFamilies()
    {
         // Major Pentatonic Family
         // 5 modes: Major Pent, Egyptian, Man Gong, Ritsusen, Minor Pentatonic
         RegisterFamily(
            "Major Pentatonic Family",
            [0, 2, 4, 7, 9],
            [
                "Major Pentatonic",
                "Egyptian (Suspended Pentatonic)",
                "Man Gong (Blues Minor / Ritsusen)", // Standard names vary
                "Ritsusen (Yo / Mode 4)",
                "Minor Pentatonic"
            ]);

         // Hirajoshi Family (Japanese)
         RegisterFamily(
             "Hirajoshi Family",
             [0, 2, 3, 7, 8],
             [
                 "Hirajoshi",
                 "Iwato",
                 "Kumoi",
                 "Hon-kumoi",
                 "Chinese" // Approximate naming convention
             ]);

         // In Sen Family
         RegisterFamily(
             "In Sen Family",
             [0, 1, 5, 7, 10],
             [
                 "In Sen",
                 "In Sen Mode 2",
                 "In Sen Mode 3",
                 "In Sen Mode 4",
                 "In Sen Mode 5"
             ]);
    }

    private static void InitializeSymmetricalFamilies()
    {
        // Whole Tone (Unique - only 1 mode really, but fits the pattern)
        RegisterFamily(
            "Whole Tone Family",
            [0, 2, 4, 6, 8, 10],
            [
                "Whole Tone",
                "Whole Tone (Inv 1)", // Symmetrical, identical intervals
                "Whole Tone (Inv 2)",
                "Whole Tone (Inv 3)",
                "Whole Tone (Inv 4)",
                "Whole Tone (Inv 5)"
            ]);

        // Diminished (Octatonic) - 2 distinct modes (Whole-Half, Half-Whole) repeating
        RegisterFamily(
            "Diminished (Octatonic) Family",
            [0, 2, 3, 5, 6, 8, 9, 11],
            [
                "Whole-Half Diminished",
                "Half-Whole Diminished (Dominant Diminished)",
                "Whole-Half Diminished (Inv 1)",
                "Half-Whole Diminished (Inv 1)",
                "Whole-Half Diminished (Inv 2)",
                "Half-Whole Diminished (Inv 2)",
                "Whole-Half Diminished (Inv 3)",
                "Half-Whole Diminished (Inv 3)"
            ]);

        // Augmented (Hexatonic) - 2 distinct modes (min3-semitone, semitone-min3) repeating
        RegisterFamily(
            "Augmented (Hexatonic) Family",
            [0, 3, 4, 7, 8, 11],
            [
                "Augmented Scale",
                "Augmented Scale (Inv 1)",
                "Augmented Scale (Inv 2)",
                "Augmented Scale (Inv 3)",
                "Augmented Scale (Inv 4)",
                "Augmented Scale (Inv 5)"
            ]);
    }

    private static void RegisterFamily(string familyName, int[] parentScale, string[] modeNames)
    {
        if (modeNames.Length != parentScale.Length)
            throw new ArgumentException($"Mode names count {modeNames.Length} does not match parent scale count {parentScale.Length}");

        var modes = new List<(string Name, int[] PitchClasses)>();

        // Sort parent to ensure it's ascending (standard set)
        Array.Sort(parentScale);

        // Calculate intervals of the parent scale
        // e.g. Major: 2, 2, 1, 2, 2, 2, 1
        var intervals = new int[parentScale.Length];
        for (var i = 0; i < parentScale.Length; i++)
        {
            var current = parentScale[i];
            var next = parentScale[(i + 1) % parentScale.Length];
            if (next < current) next += 12; // wrap around
            intervals[i] = next - current;
        }

        // Generate each rotation (Mode on C)
        for (var i = 0; i < parentScale.Length; i++)
        {
            // Rotate intervals by i
            // e.g. i=1 (Dorian): 2, 1, 2, 2, 2, 1, 2
            var rotatedIntervals = new int[parentScale.Length];
            for (var j = 0; j < parentScale.Length; j++)
            {
                rotatedIntervals[j] = intervals[(i + j) % parentScale.Length];
            }

            // Construct pitch classes from intervals starting at 0
            var modePcs = new int[parentScale.Length];
            var currentPc = 0;
            for (var k = 0; k < parentScale.Length; k++)
            {
                modePcs[k] = currentPc;
                currentPc = (currentPc + rotatedIntervals[k]) % 12;
            }
            Array.Sort(modePcs); // Ensure sorted for ID creation

            modes.Add((modeNames[i], modePcs));
        }

        // Create metadata
        var definitions = modes.ToArray();

        // Get ICV from the first mode (parent) - they all share it usually (except degenerate cases, but these are well-behaved families)
        var parentSet = new PitchClassSet(definitions[0].PitchClasses.Select(PitchClass.FromValue));
        var icv = parentSet.IntervalClassVector.ToString();

        // Create ModeFamilyMetadata similar to VoicingAnalyzer
        var noteCount = definitions[0].PitchClasses.Length;
        var names = definitions.Select(d => d.Name).ToArray();
        var ids = definitions
            .Select(d => new PitchClassSet(d.PitchClasses.Select(PitchClass.FromValue)).Id)
            .ToList();

        var metadata = new ModeFamilyMetadata(familyName, noteCount, names, ids);

        _metadata[icv] = metadata;
    }
}

public sealed record ModeFamilyMetadata(
    string FamilyName,
    int NoteCount,
    string[] ModeNames,
    List<PitchClassSetId> ModeIds);
