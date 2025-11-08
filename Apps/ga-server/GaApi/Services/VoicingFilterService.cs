namespace GaApi.Services;

using System.Collections.Immutable;
using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Fretboard.Primitives;
using Models;
// using GA.Business.Core.Fretboard.Engine // REMOVED - namespace does not exist;

/// <summary>
///     Service for filtering and ranking chord voicings
/// </summary>
public interface IVoicingFilterService
{
    /// <summary>
    ///     Gets filtered and ranked voicings for a chord
    /// </summary>
    Task<IEnumerable<VoicingWithAnalysis>> GetVoicingsForChordAsync(
        ChordTemplate template,
        PitchClass root,
        VoicingFilters filters);
}

public class VoicingFilterService(ICachingService cache, ILogger<VoicingFilterService> logger)
    : IVoicingFilterService
{
    private readonly Fretboard _fretboard = Fretboard.Default;

    public Task<IEnumerable<VoicingWithAnalysis>> GetVoicingsForChordAsync(
        ChordTemplate template,
        PitchClass root,
        VoicingFilters filters)
    {
        var fretRangeKey = filters.FretRange != null ? $"{filters.FretRange.Min}-{filters.FretRange.Max}" : "all";
        var cacheKey =
            $"voicings_{template.Name}_{root}_{filters.MaxDifficulty}_{fretRangeKey}_{filters.CagedShape}_{filters.Limit}";

        return cache.GetOrCreateRegularAsync(cacheKey, () =>
        {
            logger.LogInformation("Generating voicings for chord: {ChordName} (root: {Root})", template.Name, root);

            // Generate all possible voicings
            var generator = new FretboardChordsGenerator(_fretboard);
            var allPositions = generator.GetChordPositions(template.PitchClassSet).ToList();

            logger.LogInformation("Generated {Count} raw voicings", allPositions.Count);

            // Analyze each voicing
            var analyzed = allPositions
                .Select(positions => AnalyzeVoicing(positions, template, root))
                .Where(v => v != null)
                .Cast<VoicingWithAnalysis>()
                .ToList();

            logger.LogInformation("Analyzed {Count} voicings", analyzed.Count);

            // Apply filters
            var filtered = ApplyFilters(analyzed, filters).ToList();

            logger.LogInformation("Filtered to {Count} voicings", filtered.Count);

            // Rank by utility
            var ranked = RankByUtility(filtered, filters);

            var result = ranked.Take(filters.Limit).ToList();
            return Task.FromResult<IEnumerable<VoicingWithAnalysis>>(result);
        });
    }

    private VoicingWithAnalysis? AnalyzeVoicing(
        ImmutableList<Position> positions,
        ChordTemplate template,
        PitchClass root)
    {
        try
        {
            // Analyze with psychoacoustic analyzer
            var psychoAnalysis = PsychoacousticVoicingAnalyzer.AnalyzeVoicing(positions, _fretboard);

            // Convert to our model
            var physical = new PhysicalAnalysis
            {
                Playability = MapPlayabilityLevel(psychoAnalysis.Physical.Playability),
                FretSpan = psychoAnalysis.Physical.FretSpan,
                LowestFret = psychoAnalysis.Physical.LowestFret,
                HighestFret = psychoAnalysis.Physical.HighestFret,
                FingerStretch = psychoAnalysis.Physical.FingerStretch,
                RequiresBarre = psychoAnalysis.Physical.BarreRequirement > 0,
                HasOpenStrings = psychoAnalysis.Physical.HasOpenStrings,
                HasMutedStrings = psychoAnalysis.Physical.HasMutedStrings,
                StringCount = positions.OfType<Position.Played>().Count()
            };

            var psychoacoustic = new PsychoacousticAnalysis
            {
                Consonance = psychoAnalysis.Perceptual.ConsonanceScore,
                Brightness = psychoAnalysis.Perceptual.BrightnessIndex,
                Clarity = psychoAnalysis.Perceptual.ClarityIndex,
                HarmonicStrength = psychoAnalysis.Harmonic.FundamentalStrength,
                Register = psychoAnalysis.Harmonic.Register.ToString(),
                Density = psychoAnalysis.Textural.Density.ToString()
            };

            // Calculate utility score
            var utilityScore = CalculateUtilityScore(psychoAnalysis);

            // Determine style tags
            var styleTags = DetermineStyleTags(psychoAnalysis);

            // Detect CAGED shape (simplified - can be enhanced)
            var cagedShape = DetectCAGEDShape(positions);

            return new VoicingWithAnalysis
            {
                Positions = positions,
                Physical = physical,
                Psychoacoustic = psychoacoustic,
                Shape = cagedShape,
                UtilityScore = utilityScore,
                StyleTags = styleTags
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to analyze voicing");
            return null;
        }
    }

    private IEnumerable<VoicingWithAnalysis> ApplyFilters(
        IEnumerable<VoicingWithAnalysis> voicings,
        VoicingFilters filters)
    {
        var result = voicings;

        // Filter by difficulty
        if (filters.MaxDifficulty.HasValue)
        {
            result = result.Where(v => v.Physical.Playability <= filters.MaxDifficulty.Value);
        }

        // Filter by fret range
        if (filters.FretRange != null)
        {
            result = result.Where(v =>
                v.Physical.LowestFret >= filters.FretRange.Min &&
                v.Physical.HighestFret <= filters.FretRange.Max);
        }

        // Filter by CAGED shape
        if (filters.CagedShape.HasValue)
        {
            result = result.Where(v => v.Shape == filters.CagedShape.Value);
        }

        // Filter by open strings
        if (filters.NoOpenStrings)
        {
            result = result.Where(v => !v.Physical.HasOpenStrings);
        }

        // Filter by muted strings
        if (filters.NoMutedStrings)
        {
            result = result.Where(v => !v.Physical.HasMutedStrings);
        }

        // Filter by barres
        if (filters.NoBarres)
        {
            result = result.Where(v => !v.Physical.RequiresBarre);
        }

        // Filter by consonance
        if (filters.MinConsonance > 0)
        {
            result = result.Where(v => v.Psychoacoustic.Consonance >= filters.MinConsonance);
        }

        // Filter by style
        if (!string.IsNullOrEmpty(filters.StylePreference))
        {
            result = result.Where(v => v.StyleTags.Contains(filters.StylePreference, StringComparer.OrdinalIgnoreCase));
        }

        return result;
    }

    private IEnumerable<VoicingWithAnalysis> RankByUtility(
        IEnumerable<VoicingWithAnalysis> voicings,
        VoicingFilters filters)
    {
        // Rank by utility score, then by playability
        return voicings
            .OrderByDescending(v => v.UtilityScore)
            .ThenBy(v => v.Physical.Playability)
            .ThenBy(v => v.Physical.FretSpan);
    }

    private double CalculateUtilityScore(PsychoacousticVoicingAnalyzer.VoicingAnalysis analysis)
    {
        // Weighted combination of quality scores
        var playabilityWeight = analysis.Physical.Playability switch
        {
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Beginner => 1.0,
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Intermediate => 0.8,
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Advanced => 0.6,
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Expert => 0.4,
            _ => 0.0
        };

        var musicalQuality = analysis.Perceptual.ConsonanceScore * 0.3 +
                             analysis.Perceptual.ClarityIndex * 0.3 +
                             analysis.Harmonic.FundamentalStrength * 0.2 +
                             (1.0 - analysis.Perceptual.RoughnessIndex) * 0.2;

        // Combine playability and musical quality
        return playabilityWeight * 0.4 + musicalQuality * 0.6;
    }

    private IReadOnlyList<string> DetermineStyleTags(PsychoacousticVoicingAnalyzer.VoicingAnalysis analysis)
    {
        var tags = new List<string>();

        // Jazz voicings: complex, often with extensions, medium to high difficulty
        if (analysis.Physical.Playability >= PsychoacousticVoicingAnalyzer.PlayabilityLevel.Intermediate &&
            analysis.Harmonic.HarmonicComplexity > 0.6)
        {
            tags.Add("Jazz");
        }

        // Rock voicings: power chords, open strings, beginner to intermediate
        if (analysis.Physical.HasOpenStrings &&
            analysis.Physical.Playability <= PsychoacousticVoicingAnalyzer.PlayabilityLevel.Intermediate)
        {
            tags.Add("Rock");
        }

        // Classical voicings: balanced, clear, often fingerstyle
        if (analysis.Textural.Balance == PsychoacousticVoicingAnalyzer.VoicingBalance.Balanced &&
            analysis.Perceptual.ClarityIndex > 0.7)
        {
            tags.Add("Classical");
        }

        // Folk voicings: open strings, simple, bright
        if (analysis.Physical.HasOpenStrings &&
            analysis.Perceptual.BrightnessIndex > 0.6 &&
            analysis.Physical.Playability == PsychoacousticVoicingAnalyzer.PlayabilityLevel.Beginner)
        {
            tags.Add("Folk");
        }

        // Blues voicings: often with barre, medium difficulty
        if (analysis.Physical.BarreRequirement > 0 &&
            analysis.Physical.Playability == PsychoacousticVoicingAnalyzer.PlayabilityLevel.Intermediate)
        {
            tags.Add("Blues");
        }

        return tags;
    }

    private CagedShape? DetectCAGEDShape(ImmutableList<Position> positions)
    {
        // Simplified CAGED shape detection
        // This is a basic implementation - can be enhanced with more sophisticated pattern matching

        var playedPositions = positions.OfType<Position.Played>().ToList();
        if (!playedPositions.Any())
        {
            return null;
        }

        var lowestFret = playedPositions.Min(p => p.Location.Fret.Value);
        var hasOpenStrings = playedPositions.Any(p => p.Location.Fret.Value == 0);

        // Very basic heuristics - this should be improved
        if (hasOpenStrings)
        {
            // Open position shapes
            var bassString = playedPositions.FirstOrDefault(p => p.Location.Str.Value == 6);
            if (bassString != null)
            {
                return bassString.Location.Fret.Value switch
                {
                    0 => CagedShape.E,
                    3 => CagedShape.C,
                    _ => null
                };
            }
        }

        // Barre chord shapes (simplified)
        var barreRequirement = playedPositions
            .GroupBy(p => p.Location.Fret.Value)
            .Where(g => g.Count() >= 2)
            .Select(g => g.Key)
            .FirstOrDefault();

        if (barreRequirement > 0)
        {
            // This is a very simplified detection - should be enhanced
            return CagedShape.E; // Default to E shape for barre chords
        }

        return null;
    }

    private PlayabilityLevel MapPlayabilityLevel(PsychoacousticVoicingAnalyzer.PlayabilityLevel level)
    {
        return level switch
        {
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Beginner => PlayabilityLevel.Beginner,
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Intermediate => PlayabilityLevel.Intermediate,
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Advanced => PlayabilityLevel.Advanced,
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Expert => PlayabilityLevel.Expert,
            _ => PlayabilityLevel.Expert // Map Impossible to Expert
        };
    }
}
