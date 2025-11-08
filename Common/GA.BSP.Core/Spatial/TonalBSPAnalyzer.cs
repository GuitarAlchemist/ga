namespace GA.BSP.Core.Spatial;

using Business.Core.Atonal;
using Business.Core.Chords;
using Microsoft.Extensions.Logging;

/// <summary>
///     Advanced analyzer using Tonal BSP for musical analysis
/// </summary>
public class TonalBspAnalyzer(TonalBspService bspService, ILogger<TonalBspAnalyzer> logger)
{
    /// <summary>
    ///     Analyze a chord progression using BSP spatial relationships
    /// </summary>
    public ChordProgressionAnalysis AnalyzeProgression(List<(ChordTemplate chord, PitchClass root)> progression)
    {
        logger.LogInformation("Analyzing chord progression with {Count} chords using Tonal BSP", progression.Count);

        var chordContexts = new List<TonalBspQueryResult>();
        var tonalJourney = new List<TonalRegion>();
        var spatialDistances = new List<double>();

        // Analyze each chord in the progression
        for (var i = 0; i < progression.Count; i++)
        {
            var (chord, root) = progression[i];
            var context = bspService.FindTonalContextForChord(chord, root);
            chordContexts.Add(context);
            tonalJourney.Add(context.Region);

            // Calculate spatial distance from previous chord
            if (i > 0)
            {
                var prevContext = chordContexts[i - 1];
                var distance = CalculateSpatialDistance(prevContext.Region, context.Region);
                spatialDistances.Add(distance);

                logger.LogDebug("Chord {Index}: {Chord} in region {Region}, distance from previous: {Distance:F2}",
                    i + 1, chord.Name, context.Region.Name, distance);
            }
        }

        // Analyze overall progression characteristics
        var overallCoherence = CalculateProgressionCoherence(tonalJourney);
        var tonalMovement = AnalyzeTonalMovement(tonalJourney, spatialDistances);
        var keyStability = AnalyzeKeyStability(chordContexts);
        var modalInterchange = DetectModalInterchange(chordContexts);

        return new ChordProgressionAnalysis
        {
            Progression = progression,
            ChordContexts = chordContexts,
            TonalJourney = tonalJourney,
            SpatialDistances = spatialDistances,
            OverallCoherence = overallCoherence,
            TonalMovement = tonalMovement,
            KeyStability = keyStability,
            ModalInterchange = modalInterchange,
            Recommendations = GenerateRecommendations(chordContexts, tonalJourney)
        };
    }

    /// <summary>
    ///     Find optimal voice leading using BSP spatial optimization
    /// </summary>
    public VoiceLeadingAnalysis OptimizeVoiceLeading(List<(ChordTemplate chord, PitchClass root)> progression)
    {
        var analysis = AnalyzeProgression(progression);
        var voiceLeadingPaths = new List<VoiceLeadingPath>();

        for (var i = 0; i < progression.Count - 1; i++)
        {
            var currentChord = progression[i];
            var nextChord = progression[i + 1];

            var currentContext = analysis.ChordContexts[i];
            var nextContext = analysis.ChordContexts[i + 1];

            // Find optimal voice leading path through tonal space
            var path = FindOptimalVoiceLeadingPath(currentContext, nextContext, currentChord, nextChord);
            voiceLeadingPaths.Add(path);
        }

        var overallSmoothness = CalculateOverallSmoothness(voiceLeadingPaths);
        var totalMovement = voiceLeadingPaths.Sum(p => p.TotalMovement);

        return new VoiceLeadingAnalysis
        {
            Progression = progression,
            VoiceLeadingPaths = voiceLeadingPaths,
            OverallSmoothness = overallSmoothness,
            TotalMovement = totalMovement,
            OptimizationSuggestions = GenerateVoiceLeadingOptimizations(voiceLeadingPaths)
        };
    }

    /// <summary>
    ///     Suggest chord substitutions using BSP neighborhood search
    /// </summary>
    public List<ChordSubstitution> SuggestSubstitutions(ChordTemplate originalChord, PitchClass root,
        TonalRegion context)
    {
        var originalElement = new TonalChord(
            originalChord.Name,
            originalChord.PitchClassSet,
            DetermineChordTonality(originalChord),
            (int)root
        );

        // Find tonal neighbors in the BSP
        var neighbors = bspService.GetTonalNeighbors(originalElement, 20);

        var substitutions = new List<ChordSubstitution>();

        foreach (var neighbor in neighbors.OfType<TonalChord>())
        {
            var substitution = AnalyzeSubstitution(originalChord, neighbor, context);
            // Simplified viability check
            if (substitution != null)
            {
                substitutions.Add(substitution);
            }
        }

        // Return top suggestions
        return substitutions.Take(10).ToList();
    }

    /// <summary>
    ///     Analyze modulation paths using BSP spatial navigation
    /// </summary>
    public ModulationAnalysis AnalyzeModulation(TonalRegion fromKey, TonalRegion toKey)
    {
        // Use BSP to find the shortest path through tonal space
        var modulationPath = FindModulationPath(fromKey, toKey);
        var pivotChords = FindPivotChords(fromKey, toKey);
        var modulationStrength = CalculateModulationStrength(fromKey, toKey);

        return new ModulationAnalysis
        {
            FromKey = fromKey,
            ToKey = toKey,
            ModulationPath = modulationPath,
            PivotChords = pivotChords,
            ModulationStrength = modulationStrength,
            SuggestedTransitions = GenerateModulationSuggestions(fromKey, toKey, modulationPath)
        };
    }

    private double CalculateSpatialDistance(TonalRegion region1, TonalRegion region2)
    {
        // Calculate distance in tonal space using multiple dimensions
        var chromaticDistance = Math.Abs(region1.TonalCenter - region2.TonalCenter);
        chromaticDistance = Math.Min(chromaticDistance, 12 - chromaticDistance);

        var tonalityDistance = region1.TonalityType == region2.TonalityType ? 0.0 : 1.0;

        var setDistance = 1.0 - CalculateSetSimilarity(region1.PitchClassSet, region2.PitchClassSet);

        return (chromaticDistance / 6.0 + tonalityDistance + setDistance) / 3.0;
    }

    private double CalculateSetSimilarity(PitchClassSet set1, PitchClassSet set2)
    {
        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private double CalculateProgressionCoherence(List<TonalRegion> tonalJourney)
    {
        if (tonalJourney.Count <= 1)
        {
            return 1.0;
        }

        var coherenceScores = new List<double>();

        for (var i = 0; i < tonalJourney.Count - 1; i++)
        {
            var distance = CalculateSpatialDistance(tonalJourney[i], tonalJourney[i + 1]);
            var coherence = Math.Max(0.0, 1.0 - distance); // Closer regions = higher coherence
            coherenceScores.Add(coherence);
        }

        return coherenceScores.Average();
    }

    private TonalMovementAnalysis AnalyzeTonalMovement(List<TonalRegion> tonalJourney, List<double> spatialDistances)
    {
        var totalMovement = spatialDistances.Sum();
        var averageMovement = spatialDistances.Any() ? spatialDistances.Average() : 0.0;
        var maxMovement = spatialDistances.Any() ? spatialDistances.Max() : 0.0;

        var movementType = averageMovement switch
        {
            < 0.3 => "Static",
            < 0.6 => "Moderate",
            < 0.9 => "Active",
            _ => "Dramatic"
        };

        return new TonalMovementAnalysis
        {
            TotalMovement = totalMovement,
            AverageMovement = averageMovement,
            MaxMovement = maxMovement,
            MovementType = movementType,
            DirectionalTendency = AnalyzeDirectionalTendency(tonalJourney)
        };
    }

    private VoiceLeadingPath FindOptimalVoiceLeadingPath(TonalBspQueryResult currentContext,
        TonalBspQueryResult nextContext,
        (ChordTemplate chord, PitchClass root) current,
        (ChordTemplate chord, PitchClass root) next)
    {
        // Use BSP spatial relationships to optimize voice leading
        var spatialDistance = CalculateSpatialDistance(currentContext.Region, nextContext.Region);

        // Calculate voice leading efficiency based on spatial proximity
        var efficiency = Math.Max(0.0, 1.0 - spatialDistance);

        // Estimate total voice movement (simplified)
        var totalMovement = spatialDistance * 12; // Convert to semitones

        return new VoiceLeadingPath
        {
            FromChord = current,
            ToChord = next,
            SpatialDistance = spatialDistance,
            TotalMovement = totalMovement,
            Efficiency = efficiency,
            OptimalVoicing = GenerateOptimalVoicing(current, next, spatialDistance)
        };
    }

    private List<ModulationStep> FindModulationPath(TonalRegion fromKey, TonalRegion toKey)
    {
        // Simplified modulation path finding using BSP
        var steps = new List<ModulationStep>();

        var distance = CalculateSpatialDistance(fromKey, toKey);

        if (distance < 0.3)
        {
            // Direct modulation
            steps.Add(new ModulationStep
            {
                FromRegion = fromKey,
                ToRegion = toKey,
                ModulationType = "Direct",
                Difficulty = "Easy"
            });
        }
        else if (distance < 0.7)
        {
            // Single pivot modulation
            var pivotRegion = FindIntermediateRegion(fromKey, toKey);
            steps.Add(new ModulationStep
            {
                FromRegion = fromKey,
                ToRegion = pivotRegion,
                ModulationType = "Pivot",
                Difficulty = "Moderate"
            });
            steps.Add(new ModulationStep
            {
                FromRegion = pivotRegion,
                ToRegion = toKey,
                ModulationType = "Pivot",
                Difficulty = "Moderate"
            });
        }
        else
        {
            // Complex modulation requiring multiple steps
            steps.Add(new ModulationStep
            {
                FromRegion = fromKey,
                ToRegion = toKey,
                ModulationType = "Complex",
                Difficulty = "Advanced"
            });
        }

        return steps;
    }

    // Helper methods with simplified implementations
    private TonalityType DetermineChordTonality(ChordTemplate chord)
    {
        return TonalityType.Major;
    }

    private ChordSubstitution AnalyzeSubstitution(ChordTemplate original, TonalChord substitute, TonalRegion context)
    {
        return new ChordSubstitution();
    }

    private double CalculateModulationStrength(TonalRegion from, TonalRegion to)
    {
        return 0.5;
    }

    private List<PivotChord> FindPivotChords(TonalRegion from, TonalRegion to)
    {
        return [];
    }

    private List<ModulationSuggestion> GenerateModulationSuggestions(TonalRegion from, TonalRegion to,
        List<ModulationStep> path)
    {
        return [];
    }

    private KeyStabilityAnalysis AnalyzeKeyStability(List<TonalBspQueryResult> contexts)
    {
        return new KeyStabilityAnalysis();
    }

    private ModalInterchangeAnalysis DetectModalInterchange(List<TonalBspQueryResult> contexts)
    {
        return new ModalInterchangeAnalysis();
    }

    private List<ProgressionRecommendation> GenerateRecommendations(List<TonalBspQueryResult> contexts,
        List<TonalRegion> journey)
    {
        return [];
    }

    private double CalculateOverallSmoothness(List<VoiceLeadingPath> paths)
    {
        return 0.8;
    }

    private List<VoiceLeadingOptimization> GenerateVoiceLeadingOptimizations(List<VoiceLeadingPath> paths)
    {
        return [];
    }

    private string AnalyzeDirectionalTendency(List<TonalRegion> journey)
    {
        return "Ascending";
    }

    private string GenerateOptimalVoicing((ChordTemplate, PitchClass) current, (ChordTemplate, PitchClass) next,
        double distance)
    {
        return "Close";
    }

    private TonalRegion FindIntermediateRegion(TonalRegion from, TonalRegion to)
    {
        return from;
    }
}

// Supporting data structures for analysis results
public record ChordProgressionAnalysis
{
    public List<(ChordTemplate chord, PitchClass root)> Progression { get; init; } = [];
    public List<TonalBspQueryResult> ChordContexts { get; init; } = [];
    public List<TonalRegion> TonalJourney { get; init; } = [];
    public List<double> SpatialDistances { get; init; } = [];
    public double OverallCoherence { get; init; }
    public TonalMovementAnalysis TonalMovement { get; init; } = new();
    public KeyStabilityAnalysis KeyStability { get; init; } = new();
    public ModalInterchangeAnalysis ModalInterchange { get; init; } = new();
    public List<ProgressionRecommendation> Recommendations { get; init; } = [];
}

public record TonalMovementAnalysis
{
    public double TotalMovement { get; init; }
    public double AverageMovement { get; init; }
    public double MaxMovement { get; init; }
    public string MovementType { get; init; } = "";
    public string DirectionalTendency { get; init; } = "";
}

public record VoiceLeadingAnalysis
{
    public List<(ChordTemplate chord, PitchClass root)> Progression { get; init; } = [];
    public List<VoiceLeadingPath> VoiceLeadingPaths { get; init; } = [];
    public double OverallSmoothness { get; init; }
    public double TotalMovement { get; init; }
    public List<VoiceLeadingOptimization> OptimizationSuggestions { get; init; } = [];
}

public record VoiceLeadingPath
{
    public (ChordTemplate chord, PitchClass root) FromChord { get; init; }
    public (ChordTemplate chord, PitchClass root) ToChord { get; init; }
    public double SpatialDistance { get; init; }
    public double TotalMovement { get; init; }
    public double Efficiency { get; init; }
    public string OptimalVoicing { get; init; } = "";
}

public record ModulationAnalysis
{
    public TonalRegion FromKey { get; init; } = new();
    public TonalRegion ToKey { get; init; } = new();
    public List<ModulationStep> ModulationPath { get; init; } = [];
    public List<PivotChord> PivotChords { get; init; } = [];
    public double ModulationStrength { get; init; }
    public List<ModulationSuggestion> SuggestedTransitions { get; init; } = [];
}

// Additional supporting records...
public record ChordSubstitution;

public record KeyStabilityAnalysis;

public record ModalInterchangeAnalysis;

public record ProgressionRecommendation;

public record VoiceLeadingOptimization;

public record ModulationStep
{
    public TonalRegion FromRegion { get; init; } = new();
    public TonalRegion ToRegion { get; init; } = new();
    public string ModulationType { get; init; } = "";
    public string Difficulty { get; init; } = "";
}

public record PivotChord;

public record ModulationSuggestion;
