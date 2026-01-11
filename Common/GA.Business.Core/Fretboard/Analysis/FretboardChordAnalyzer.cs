namespace GA.Business.Core.Fretboard.Analysis;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Atonal;
using Biomechanics;
using Chords;
using Intervals;
using Primitives;

/// <summary>
///     Analyzes chord voicings on the fretboard for ergonomics, voice leading, and musical properties
/// </summary>
public static class FretboardChordAnalyzer
{
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
            .Select(pos => fretboard.GetNote(pos.Location.Str.Value - 1, pos.Location.Fret.Value).PitchClass)
            .ToHashSet();

        var pcs = new PitchClassSet(pitchClasses);
        var consonance = CalculateConsonance(pcs);
        var tension = CalculateTension(pcs);

        // Create a simple analytical chord template
        var majorFormula = CommonChordFormulas.Major;
        var chordTemplate = new ChordTemplate.Analytical(majorFormula, "Simple Major", pcs);

        return new(
            pcs,
            chordTemplate,
            ImmutableArray<Interval>.Empty,
            consonance,
            tension,
            "Unknown"); // Would need chord symbol generation
    }

    private static VoiceLeadingAnalysis AnalyzeVoiceLeading(ImmutableArray<Position> voicing)
    {
        // Simplified - for single chord analysis
        return new(
            ImmutableArray<VoiceMovement>.Empty,
            1.0,
            0.0,
            0.0);
    }

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
    private static double CalculateDifficultyScore(Position[] positions)
    {
        return Math.Min(1.0, positions.Length * 0.1 + CalculateSpread(positions) * 0.05);
    }

    private static double CalculateStretchFactor(Position[] positions)
    {
        return positions.Length < 2
            ? 0.0
            : Math.Min(1.0,
                (positions.Max(p => p.Location.Fret.Value) - positions.Min(p => p.Location.Fret.Value)) / 5.0);
    }

    private static double CalculateBarreComplexity(Position[] positions)
    {
        return positions.GroupBy(p => p.Location.Fret.Value).Count(g => g.Count() > 1) * 0.3;
    }

    private static double CalculateSpread(Position[] positions)
    {
        return positions.Length < 2
            ? 0.0
            : positions.Max(p => p.Location.Str.Value) - positions.Min(p => p.Location.Str.Value);
    }

    private static ImmutableArray<FingerPosition> GenerateFingerPositions(Position[] positions)
    {
        return [.. positions.Select((pos, i) => new FingerPosition(
            (FingerType)(i % 4 + 1), // Simplified finger assignment
            pos.Location.Str.Value,
            pos.Location.Fret.Value,
            0.5f,
            false))];
    }

    private static double CalculateConsonance(PitchClassSet pcs)
    {
        return Math.Max(0.0, 1.0 - pcs.IntervalClassVector.Sum() / 12.0);
    }

    private static double CalculateTension(PitchClassSet pcs)
    {
        return pcs.IntervalClassVector.Sum() / 12.0;
    }

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
        ImmutableArray<string> Recommendations);

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
        ChordTemplate ChordTemplate,
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
