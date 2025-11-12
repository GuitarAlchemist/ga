namespace GA.Business.Core.Fretboard.Biomechanics;

using Primitives;

/// <summary>
///     Analyzer for biomechanical playability of chord fingerings
/// </summary>
public class BiomechanicalAnalyzer(HandSize handSize = HandSize.Medium)
{
    private readonly HandSize _handSize = handSize;

    /// <summary>
    ///     Analyze the biomechanical playability of a chord fingering
    /// </summary>
    public BiomechanicalPlayabilityAnalysis AnalyzeChordPlayability(ImmutableList<Position> positions)
    {
        // Extract fret positions for analysis
        var fretPositions = ExtractFretPositions(positions);

        if (fretPositions.Count == 0)
        {
            return CreateEmptyAnalysis();
        }

        // Analyze finger stretch
        var stretchAnalysis = AnalyzeStretch(fretPositions);

        // Analyze fingering efficiency
        var efficiencyAnalysis = AnalyzeFingeringEfficiency(fretPositions);

        // Analyze wrist posture
        var wristAnalysis = AnalyzeWristPosture(fretPositions);

        // Analyze muting (stub for now)
        var mutingAnalysis = AnalyzeMuting(fretPositions);

        // Analyze slide/legato (stub for now)
        var slideLegatoAnalysis = AnalyzeSlideLegato(fretPositions);

        // Compute best hand pose (stub for now)
        var bestPose = ComputeBestHandPose(fretPositions);

        // Calculate component scores
        var reachability = CalculateReachability(stretchAnalysis);
        var comfort = CalculateComfort(stretchAnalysis, wristAnalysis);
        var naturalness = CalculateNaturalness(wristAnalysis);
        var efficiency = efficiencyAnalysis?.EfficiencyScore ?? 0.5;
        var stability = CalculateStability(fretPositions);

        // Calculate overall score
        var overallScore = (reachability + comfort + naturalness + efficiency + stability) / 5.0;

        // Determine difficulty
        var difficulty = DetermineDifficulty(overallScore);

        // Determine if playable
        var isPlayable = overallScore >= 0.3;

        return new BiomechanicalPlayabilityAnalysis(
            overallScore,
            difficulty,
            isPlayable,
            reachability,
            comfort,
            naturalness,
            efficiency,
            stability,
            stretchAnalysis,
            efficiencyAnalysis,
            wristAnalysis,
            mutingAnalysis,
            slideLegatoAnalysis,
            bestPose
        );
    }

    private List<(int String, int Fret)> ExtractFretPositions(ImmutableList<Position> positions)
    {
        var fretPositions = new List<(int String, int Fret)>();

        for (var i = 0; i < positions.Count; i++)
        {
            if (positions[i] is Position.Played played)
            {
                fretPositions.Add((i, played.Location.Fret));
            }
        }

        return fretPositions;
    }

    private StretchAnalysis AnalyzeStretch(List<(int String, int Fret)> fretPositions)
    {
        if (fretPositions.Count == 0)
        {
            return new StretchAnalysis(0, 0, "No notes", false, 0);
        }

        var minFret = fretPositions.Min(p => p.Fret);
        var maxFret = fretPositions.Max(p => p.Fret);
        var fretSpan = maxFret - minFret;

        // Estimate stretch distance (rough approximation)
        var stretchDistance = fretSpan * 25.0; // ~25mm per fret

        var hasWideStretches = fretSpan > 4;
        var wideStretchCount = hasWideStretches ? 1 : 0;

        var description = fretSpan switch
        {
            0 => "No stretch required",
            1 => "Minimal stretch",
            2 => "Comfortable stretch",
            3 => "Moderate stretch",
            4 => "Wide stretch",
            _ => "Very wide stretch"
        };

        return new StretchAnalysis(
            stretchDistance,
            fretSpan,
            description,
            hasWideStretches,
            wideStretchCount
        );
    }

    private FingeringEfficiencyAnalysis? AnalyzeFingeringEfficiency(List<(int String, int Fret)> fretPositions)
    {
        if (fretPositions.Count == 0)
        {
            return null;
        }

        var minFret = fretPositions.Min(p => p.Fret);
        var maxFret = fretPositions.Max(p => p.Fret);
        var fingerSpan = maxFret - minFret;

        // Simple efficiency calculation
        var efficiencyScore = fingerSpan switch
        {
            0 => 1.0,
            1 => 0.95,
            2 => 0.85,
            3 => 0.7,
            4 => 0.5,
            _ => 0.3
        };

        var pinkyUsage = fingerSpan > 3 ? 25.0 : 0.0;
        var hasBarreChord = fretPositions.GroupBy(p => p.Fret).Any(g => g.Count() >= 3);
        var usesThumb = false; // Simplified

        var recommendations = new List<string>();
        if (fingerSpan > 4)
        {
            recommendations.Add("Consider using a different voicing with smaller span");
        }

        if (hasBarreChord)
        {
            recommendations.Add("Practice barre technique for this chord");
        }

        var fingerUsageCounts = new Dictionary<FingerType, int>
        {
            [FingerType.Thumb] = 0,
            [FingerType.Index] = 0,
            [FingerType.Middle] = 0,
            [FingerType.Ring] = 0,
            [FingerType.Little] = 0
        };

        return new FingeringEfficiencyAnalysis(
            fingerUsageCounts,
            efficiencyScore,
            pinkyUsage,
            fingerSpan,
            hasBarreChord,
            usesThumb,
            $"Finger span of {fingerSpan} frets",
            recommendations.ToImmutableList()
        );
    }

    private WristPostureAnalysis AnalyzeWristPosture(List<(int String, int Fret)> fretPositions)
    {
        if (fretPositions.Count == 0)
        {
            return new WristPostureAnalysis(0, PostureType.Neutral, true);
        }

        // Simplified wrist angle calculation
        var avgFret = fretPositions.Average(p => p.Fret);
        var wristAngle = avgFret < 5 ? 15.0 : avgFret < 12 ? 10.0 : 5.0;

        var postureType = wristAngle switch
        {
            < 5 => PostureType.Neutral,
            < 10 => PostureType.SlightlyFlexed,
            < 20 => PostureType.Flexed,
            _ => PostureType.Extended
        };

        var isErgonomic = wristAngle < 20;

        return new WristPostureAnalysis(wristAngle, postureType, isErgonomic);
    }

    private MutingAnalysis? AnalyzeMuting(List<(int String, int Fret)> fretPositions)
    {
        // Stub implementation
        return null;
    }

    private SlideLegatoAnalysis? AnalyzeSlideLegato(List<(int String, int Fret)> fretPositions)
    {
        // Stub implementation
        return null;
    }

    private HandPose? ComputeBestHandPose(List<(int String, int Fret)> fretPositions)
    {
        // Stub implementation - would use IK solver
        return null;
    }

    private double CalculateReachability(StretchAnalysis stretchAnalysis)
    {
        return stretchAnalysis.MaxFretSpan switch
        {
            0 => 1.0,
            1 => 0.95,
            2 => 0.85,
            3 => 0.7,
            4 => 0.5,
            _ => 0.3
        };
    }

    private double CalculateComfort(StretchAnalysis stretchAnalysis, WristPostureAnalysis wristAnalysis)
    {
        var stretchComfort = 1.0 - stretchAnalysis.MaxFretSpan / 10.0;
        var wristComfort = wristAnalysis.IsErgonomic ? 1.0 : 0.5;
        return (stretchComfort + wristComfort) / 2.0;
    }

    private double CalculateNaturalness(WristPostureAnalysis wristAnalysis)
    {
        return wristAnalysis.PostureType switch
        {
            PostureType.Neutral => 1.0,
            PostureType.SlightlyFlexed => 0.9,
            PostureType.Flexed => 0.7,
            PostureType.SlightlyExtended => 0.8,
            PostureType.Extended => 0.5,
            _ => 0.5
        };
    }

    private double CalculateStability(List<(int String, int Fret)> fretPositions)
    {
        // More notes generally means more stability
        var noteCount = fretPositions.Count;
        return Math.Min(1.0, noteCount / 6.0);
    }

    private string DetermineDifficulty(double overallScore)
    {
        return overallScore switch
        {
            >= 0.9 => "Very Easy",
            >= 0.7 => "Easy",
            >= 0.5 => "Moderate",
            >= 0.3 => "Difficult",
            _ => "Very Difficult"
        };
    }

    private BiomechanicalPlayabilityAnalysis CreateEmptyAnalysis()
    {
        return new BiomechanicalPlayabilityAnalysis(
            0,
            "N/A",
            false,
            0,
            0,
            0,
            0,
            0,
            null,
            null,
            null,
            null,
            null,
            null
        );
    }
}
