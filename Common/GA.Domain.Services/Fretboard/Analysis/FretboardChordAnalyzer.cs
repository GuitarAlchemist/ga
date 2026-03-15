namespace GA.Domain.Services.Fretboard.Analysis;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Core.Instruments.Biomechanics;
using Core.Instruments.Fretboard.Analysis;
using Core.Instruments.Fretboard.Voicings.Core;
using Core.Instruments.Positions;
using Core.Instruments.Primitives;
using Core.Primitives.Intervals;
using Core.Primitives.Notes;
using Core.Theory.Atonal;
using Core.Theory.Harmony;
using Voicings.Generation;
using ServicesChordTemplate = GA.Domain.Core.Theory.Harmony.ChordTemplate;

/// <summary>
///     Analyzes chord voicings on the fretboard for ergonomics, voice leading, and musical properties
/// </summary>
public static class FretboardChordAnalyzer
{
    /// <summary>
    ///     Generates all chord voicings within 5-fret spans across the entire fretboard.
    /// </summary>
    public static IEnumerable<FiveFretSpanChord> GenerateAllFiveFretSpanChords(Fretboard fretboard) =>
        GenerateAllFiveFretSpanChords(fretboard, fretboard.FretCount);

    /// <summary>
    ///     Generates all chord voicings within 5-fret spans up to the specified maximum fret.
    /// </summary>
    public static IEnumerable<FiveFretSpanChord> GenerateAllFiveFretSpanChords(
        Fretboard fretboard,
        int maxFret = 24,
        int minPlayedNotes = 3)
    {
        const int windowSize = 5;
        var stringCount = fretboard.StringCount;
        var maxStartFret = Math.Max(0, maxFret - windowSize);

        // Pre-cache instances for VoicingGenerator
        var cachedFrets = Fret.ItemsSpan.ToArray();
        var cachedStrings = Str.Range(stringCount).ToArray();
        var cachedMutedPositions = cachedStrings.Select(s => new Position.Muted(s)).ToArray();
        var fretMin = Fret.Min.Value;
        var maxFretCount = Math.Min(fretboard.FretCount, maxFret);
        var cachedLocations = new PositionLocation[stringCount, maxFretCount + 1];
        for (var s = 0; s < stringCount; s++)
        {
            for (var f = 0; f <= maxFretCount; f++)
            {
                cachedLocations[s, f] = new(cachedStrings[s], cachedFrets[f - fretMin]);
            }
        }

        var seenDiagrams = new HashSet<string>();

        for (var startFret = 0; startFret <= maxStartFret; startFret++)
        {
            var endFret = Math.Min(startFret + windowSize, maxFretCount);
            var voicings = VoicingGenerator.GenerateAllVoicingsInWindowOptimized(
                fretboard, startFret, endFret,
                cachedFrets, cachedStrings, cachedMutedPositions, cachedLocations,
                minPlayedNotes, windowSize);

            foreach (var voicing in voicings)
            {
                var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);
                if (!seenDiagrams.Add(diagram)) continue;

                // Compute fret bounds
                var lowestFret = int.MaxValue;
                var highestFret = 0;
                var frets = new int[stringCount];

                for (var i = 0; i < stringCount; i++)
                {
                    switch (voicing.Positions[i])
                    {
                        case Position.Played played:
                            var fretVal = played.Location.Fret.Value;
                            frets[i] = fretVal;
                            if (fretVal > 0 && fretVal < lowestFret) lowestFret = fretVal;
                            if (fretVal > highestFret) highestFret = fretVal;
                            break;
                        default:
                            frets[i] = -1; // Muted
                            break;
                    }
                }

                if (lowestFret == int.MaxValue) lowestFret = 0;

                var invariant = ChordInvariant.FromFrets(frets, fretboard.Tuning);

                // Derive chord name from pitch classes
                var chordName = DeriveChordName(voicing.Notes, fretboard);

                yield return new(
                    [..voicing.Positions],
                    invariant,
                    lowestFret,
                    highestFret,
                    chordName);
            }
        }
    }

    /// <summary>
    ///     Derives a simple chord name from a set of MIDI notes.
    /// </summary>
    private static string DeriveChordName(MidiNote[] notes, Fretboard fretboard)
    {
        if (notes.Length == 0) return "N/C";

        var pitchClasses = notes
            .Select(n => (int)n.Value % 12)
            .Distinct()
            .OrderBy(pc => pc)
            .ToArray();

        // Map pitch class to note name
        var root = pitchClasses[0];
        var rootName = root switch
        {
            0 => "C", 1 => "C#", 2 => "D", 3 => "D#", 4 => "E", 5 => "F",
            6 => "F#", 7 => "G", 8 => "G#", 9 => "A", 10 => "A#", 11 => "B",
            _ => "?"
        };

        if (pitchClasses.Length < 3) return rootName;

        // Check intervals from root to identify quality
        var intervals = pitchClasses.Skip(1).Select(pc => (pc - root + 12) % 12).OrderBy(i => i).ToArray();

        return intervals switch
        {
            [4, 7] => rootName,          // Major triad
            [3, 7] => rootName + "m",    // Minor triad
            [3, 6] => rootName + "dim",  // Diminished
            [4, 8] => rootName + "aug",  // Augmented
            [5, 7] => rootName + "sus4", // sus4
            [2, 7] => rootName + "sus2", // sus2
            _ => rootName + "?"
        };
    }

    /// <summary>
    ///     Analyzes a chord voicing on the fretboard
    /// </summary>
    public static FretboardChordAnalysis AnalyzeChordVoicing(
        IEnumerable<Position> voicing,
        Fretboard fretboard,
        bool includeBiomechanicalAnalysis = true)
    {
        ArgumentNullException.ThrowIfNull(voicing);
        ArgumentNullException.ThrowIfNull(fretboard);

        var voicingArray = voicing.ToImmutableArray();

        // Analyze ergonomics
        var ergonomics = AnalyzeErgonomics(voicingArray, fretboard, includeBiomechanicalAnalysis);

        // Analyze harmonics
        var harmonics = AnalyzeHarmonics(voicingArray, fretboard);

        // Analyze voice leading (simplified for single chord)
        var voiceLeading = AnalyzeVoiceLeading(voicingArray);

        // Calculate overall score
        var overallScore = CalculateOverallScore(ergonomics, harmonics, voiceLeading);

        // Generate recommendations
        var recommendations = GenerateRecommendations(ergonomics, harmonics, voiceLeading);

        return new(
            voicingArray,
            fretboard,
            ergonomics,
            harmonics,
            voiceLeading,
            overallScore,
            recommendations);
    }

    private static ErgonomicAnalysis AnalyzeErgonomics(ImmutableArray<Position> voicing, Fretboard fretboard,
        bool includeBiomechanics)
    {
        var positions = voicing.ToArray();
        var difficultyScore = CalculateDifficultyScore(positions);
        var stretchFactor = CalculateStretchFactor(positions);
        var barreComplexity = CalculateBarreComplexity(positions);

        HandPose? optimalPose = null;
        var fingerPositions = ImmutableArray<FingerPosition>.Empty;
        var isPlayable = true;

        if (includeBiomechanics)
        {
            // Simplified biomechanical analysis
            optimalPose = HandPose.CreateRestPose(HandModel.CreateStandardAdult());
            fingerPositions = GenerateFingerPositions(positions);
            isPlayable = difficultyScore < 0.8; // Threshold for playability
        }

        return new(
            difficultyScore,
            stretchFactor,
            barreComplexity,
            optimalPose ?? HandPose.CreateRestPose(HandModel.CreateStandardAdult()),
            fingerPositions,
            isPlayable);
    }

    private static HarmonicAnalysis AnalyzeHarmonics(ImmutableArray<Position> voicing, Fretboard fretboard)
    {
        var pitchClasses = voicing
            .OfType<Position.Played>()
            .Select(pos => fretboard.GetNote(pos.Location.Str.Value - 1, pos.Location.Fret.Value).PitchClass)
            .ToHashSet();

        var pcs = new PitchClassSet(pitchClasses);
        var consonance = CalculateConsonance(pcs);
        var tension = CalculateTension(pcs);

        // Create a simple analytical chord template
        var majorFormula = CommonChordFormulas.Major;
        var chordTemplate = new ServicesChordTemplate.Analytical(pcs, majorFormula, "Simple Major");

        return new(
            pcs,
            chordTemplate,
            [],
            consonance,
            tension,
            "Unknown"); // Would need chord symbol generation
    }

    private static VoiceLeadingAnalysis AnalyzeVoiceLeading(ImmutableArray<Position> voicing) =>
        // Simplified - for single chord analysis
        new(
            [],
            1.0,
            0.0,
            0.0);

    private static double CalculateOverallScore(ErgonomicAnalysis ergonomics, HarmonicAnalysis harmonics,
        VoiceLeadingAnalysis voiceLeading)
    {
        var ergonomicWeight = 0.4;
        var harmonicWeight = 0.4;
        var voiceLeadingWeight = 0.2;

        var ergonomicScore = 1.0 - ergonomics.DifficultyScore;
        var harmonicScore = harmonics.Consonance;
        var voiceLeadingScore = voiceLeading.SmoothnesScore;

        return ergonomicScore * ergonomicWeight +
               harmonicScore * harmonicWeight +
               voiceLeadingScore * voiceLeadingWeight;
    }

    private static ImmutableArray<string> GenerateRecommendations(ErgonomicAnalysis ergonomics,
        HarmonicAnalysis harmonics, VoiceLeadingAnalysis voiceLeading)
    {
        var recommendations = new List<string>();

        if (ergonomics.DifficultyScore > 0.7)
        {
            recommendations.Add("Consider alternative fingering for easier execution");
        }

        if (ergonomics.StretchFactor > 0.8)
        {
            recommendations.Add("Large finger stretch required - practice slowly");
        }

        if (harmonics.Tension > 0.6)
        {
            recommendations.Add("High harmonic tension - consider resolution");
        }

        return [.. recommendations];
    }

    // Helper methods with simplified implementations
    private static double CalculateDifficultyScore(Position[] positions) => Math.Min(1.0, positions.Length * 0.1 + CalculateSpread(positions) * 0.05);

    private static double CalculateStretchFactor(Position[] positions) =>
        positions.Length < 2
            ? 0.0
            : Math.Min(1.0,
                (positions.Max(p => p.Location.Fret.Value) - positions.Min(p => p.Location.Fret.Value)) / 5.0);

    private static double CalculateBarreComplexity(Position[] positions) => positions.GroupBy(p => p.Location.Fret.Value).Count(g => g.Count() > 1) * 0.3;

    private static double CalculateSpread(Position[] positions) =>
        positions.Length < 2
            ? 0.0
            : positions.Max(p => p.Location.Str.Value) - positions.Min(p => p.Location.Str.Value);

    private static ImmutableArray<FingerPosition> GenerateFingerPositions(Position[] positions) =>
    [.. positions.Select((pos, i) => new FingerPosition(
        (FingerType)(i % 4 + 1), // Simplified finger assignment
        pos.Location.Str.Value,
        pos.Location.Fret.Value,
        0.5f,
        false))];

    private static double CalculateConsonance(PitchClassSet pcs) => Math.Max(0.0, 1.0 - pcs.IntervalClassVector.Sum() / 12.0);

    private static double CalculateTension(PitchClassSet pcs) => pcs.IntervalClassVector.Sum() / 12.0;

    /// <summary>
    ///     Comprehensive analysis result for a fretboard chord voicing
    /// </summary>
    public record FretboardChordAnalysis(
        ImmutableArray<Position> Voicing,
        Fretboard Fretboard,
        ErgonomicAnalysis Ergonomics,
        HarmonicAnalysis Harmonics,
        VoiceLeadingAnalysis VoiceLeading,
        double OverallScore,
        ImmutableArray<string> Recommendations)
    {
        /// <summary>Chord template from harmonic analysis.</summary>
        public ServicesChordTemplate ChordTemplate => Harmonics.ChordTemplate;

        /// <summary>Chord symbol from harmonic analysis.</summary>
        public string ChordName => Harmonics.ChordSymbol;

        /// <summary>Root note (bass note of the voicing).</summary>
        public Note? Root
        {
            get
            {
                var playedPositions = Voicing
                    .OfType<Position.Played>()
                    .OrderBy(p => p.MidiNote.Value)
                    .ToList();

                if (playedPositions.Count == 0) return null;

                var bassNote = playedPositions[0];
                var stringIndex = bassNote.Location.Str.Value - 1;
                var fret = bassNote.Location.Fret.Value;
                return Fretboard.GetNote(stringIndex, fret);
            }
        }
    }

    /// <summary>
    ///     Ergonomic analysis of finger positioning and hand biomechanics
    /// </summary>
    public record ErgonomicAnalysis(
        double DifficultyScore,
        double StretchFactor,
        double BarreComplexity,
        HandPose OptimalHandPose,
        ImmutableArray<FingerPosition> FingerPositions,
        bool IsPlayable);

    /// <summary>
    ///     Harmonic analysis of the chord voicing
    /// </summary>
    public record HarmonicAnalysis(
        PitchClassSet PitchClasses,
        ServicesChordTemplate ChordTemplate,
        ImmutableArray<Interval> Intervals,
        double Consonance,
        double Tension,
        string ChordSymbol);

    /// <summary>
    ///     Voice leading analysis for chord transitions
    /// </summary>
    public record VoiceLeadingAnalysis(
        ImmutableArray<VoiceMovement> VoiceMovements,
        double SmoothnesScore,
        double ParallelMotionPenalty,
        double LeapPenalty);

    /// <summary>
    ///     Individual voice movement in a chord transition
    /// </summary>
    public record VoiceMovement(
        int StringIndex,
        int FromFret,
        int ToFret,
        Interval IntervalMovement,
        VoiceMotionType MotionType);

    /// <summary>
    ///     Finger position on the fretboard
    /// </summary>
    public record FingerPosition(
        FingerType Finger,
        int StringIndex,
        int Fret,
        float Pressure,
        bool IsBarre);
}

/// <summary>
///     Types of voice motion between chords
/// </summary>
public enum VoiceMotionType
{
    Static,
    Step,
    Skip,
    Leap
}
