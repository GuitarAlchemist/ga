namespace GA.Business.Core.Fretboard.Voicings.Analysis;

using Positions;
using Primitives;

/// <summary>
/// Analyzes voicings using psychoacoustic principles
/// </summary>
public static class PsychoacousticVoicingAnalyzer
{
    /// <summary>
    /// Analyze a voicing comprehensively
    /// </summary>
    public static VoicingAnalysis AnalyzeVoicing(
        IEnumerable<PositionLocation> positions,
        Fretboard fretboard)
    {
        var positionsList = positions.ToList();

        var physical = AnalyzePhysical(positionsList);
        var perceptual = AnalyzePerceptual(positionsList, fretboard);
        var textural = AnalyzeTextural(positionsList);
        var harmonic = AnalyzeHarmonic(positionsList, fretboard);
        var quality = ComputeQuality(physical, perceptual, textural, harmonic);
        var semanticTags = GenerateSemanticTags(physical, perceptual, textural);

        return new VoicingAnalysis
        {
            Physical = physical,
            Perceptual = perceptual,
            Textural = textural,
            Harmonic = harmonic,
            Quality = quality,
            SemanticTags = semanticTags
        };
    }

    private static PhysicalAnalysis AnalyzePhysical(List<PositionLocation> positions)
    {
        if (!positions.Any())
        {
            return new PhysicalAnalysis
            {
                Playability = PlayabilityLevel.Beginner,
                FretSpan = 0,
                FingerStretch = 0.0,
                BarreRequired = false
            };
        }

        var fretSpan = positions.Max(p => p.Fret.Value) - positions.Min(p => p.Fret.Value);
        var fingerStretch = fretSpan / 5.0; // Normalize to 0-1
        var barreRequired = positions.GroupBy(p => p.Fret.Value).Any(g => g.Count() >= 3);

        var playability = (fretSpan, fingerStretch) switch
        {
            ( <= 3, <= 0.4) => PlayabilityLevel.Beginner,
            ( <= 4, <= 0.6) => PlayabilityLevel.Intermediate,
            ( <= 5, <= 0.8) => PlayabilityLevel.Advanced,
            _ => PlayabilityLevel.Expert
        };

        return new PhysicalAnalysis
        {
            Playability = playability,
            FretSpan = fretSpan,
            FingerStretch = fingerStretch,
            BarreRequired = barreRequired
        };
    }

    private static PerceptualAnalysis AnalyzePerceptual(List<PositionLocation> positions, Fretboard fretboard)
    {
        if (!positions.Any())
        {
            return new PerceptualAnalysis
            {
                ConsonanceScore = 0.0,
                BrightnessIndex = 0.0,
                Weight = VoicingWeight.Light
            };
        }

        var notes = positions.Select(p => fretboard.GetNote(p.Str.Value - 1, p.Fret.Value)).ToList();
        var avgPitch = notes.Average(n => (int)n.PitchClass);

        var consonanceScore = 0.7; // Placeholder
        var brightnessIndex = avgPitch / 127.0;
        var weight = avgPitch switch
        {
            < 50 => VoicingWeight.Heavy,
            < 65 => VoicingWeight.Medium,
            _ => VoicingWeight.Light
        };

        return new PerceptualAnalysis
        {
            ConsonanceScore = consonanceScore,
            BrightnessIndex = brightnessIndex,
            Weight = weight
        };
    }

    private static TexturalAnalysis AnalyzeTextural(List<PositionLocation> positions)
    {
        var density = positions.Count switch
        {
            <= 3 => VoicingDensity.Sparse,
            <= 4 => VoicingDensity.Medium,
            _ => VoicingDensity.Dense
        };

        var spacing = VoicingSpacing.Close; // Placeholder
        var color = VoicingColor.Warm; // Placeholder

        return new TexturalAnalysis
        {
            Density = density,
            Spacing = spacing,
            Color = color
        };
    }

    private static HarmonicAnalysis AnalyzeHarmonic(List<PositionLocation> positions, Fretboard fretboard)
    {
        return new HarmonicAnalysis
        {
            RootPosition = true,
            Inversion = 0,
            VoiceLeadingSmooth = true
        };
    }

    private static QualityAnalysis ComputeQuality(
        PhysicalAnalysis physical,
        PerceptualAnalysis perceptual,
        TexturalAnalysis textural,
        HarmonicAnalysis harmonic)
    {
        var playabilityScore = physical.Playability switch
        {
            PlayabilityLevel.Beginner => 1.0,
            PlayabilityLevel.Intermediate => 0.8,
            PlayabilityLevel.Advanced => 0.6,
            PlayabilityLevel.Expert => 0.4,
            _ => 0.5
        };

        var overallQuality = (playabilityScore + perceptual.ConsonanceScore) / 2.0;

        return new QualityAnalysis
        {
            OverallQuality = overallQuality,
            PlayabilityScore = playabilityScore,
            MusicalityScore = perceptual.ConsonanceScore
        };
    }

    private static List<string> GenerateSemanticTags(
        PhysicalAnalysis physical,
        PerceptualAnalysis perceptual,
        TexturalAnalysis textural)
    {
        var tags = new List<string>
        {
            physical.Playability.ToString().ToLower(),
            perceptual.Weight.ToString().ToLower(),
            textural.Density.ToString().ToLower()
        };

        if (physical.BarreRequired) tags.Add("barre");
        if (perceptual.BrightnessIndex > 0.7) tags.Add("bright");
        if (perceptual.BrightnessIndex < 0.3) tags.Add("dark");

        return tags;
    }
}

/// <summary>
/// Complete voicing analysis
/// </summary>
public record VoicingAnalysis
{
    public required PhysicalAnalysis Physical { get; init; }
    public required PerceptualAnalysis Perceptual { get; init; }
    public required TexturalAnalysis Textural { get; init; }
    public required HarmonicAnalysis Harmonic { get; init; }
    public required QualityAnalysis Quality { get; init; }
    public required List<string> SemanticTags { get; init; }
}

public record PhysicalAnalysis
{
    public required PlayabilityLevel Playability { get; init; }
    public required int FretSpan { get; init; }
    public required double FingerStretch { get; init; }
    public required bool BarreRequired { get; init; }
}

public record PerceptualAnalysis
{
    public required double ConsonanceScore { get; init; }
    public required double BrightnessIndex { get; init; }
    public required VoicingWeight Weight { get; init; }
}

public record TexturalAnalysis
{
    public required VoicingDensity Density { get; init; }
    public required VoicingSpacing Spacing { get; init; }
    public required VoicingColor Color { get; init; }
}

public record HarmonicAnalysis
{
    public required bool RootPosition { get; init; }
    public required int Inversion { get; init; }
    public required bool VoiceLeadingSmooth { get; init; }
}

public record QualityAnalysis
{
    public required double OverallQuality { get; init; }
    public required double PlayabilityScore { get; init; }
    public required double MusicalityScore { get; init; }
}

public enum PlayabilityLevel
{
    Beginner,
    Intermediate,
    Advanced,
    Expert
}

public enum VoicingWeight
{
    Light,
    Medium,
    Heavy
}

public enum VoicingDensity
{
    Sparse,
    Medium,
    Dense
}

public enum VoicingSpacing
{
    Close,
    Medium,
    Wide
}

public enum VoicingColor
{
    Warm,
    Neutral,
    Bright
}

