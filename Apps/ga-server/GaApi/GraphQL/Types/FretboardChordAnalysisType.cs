namespace GaApi.GraphQL.Types;

using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Biomechanics;

/// <summary>
///     GraphQL type for fretboard chord analysis
/// </summary>
public class FretboardChordAnalysisType
{
    public string ChordName { get; set; } = "";
    public string HybridAnalysisName { get; set; } = "";
    public string? IconicName { get; set; }
    public string? IconicDescription { get; set; }
    public int FretSpan { get; set; }
    public int LowestFret { get; set; }
    public int HighestFret { get; set; }
    public string Difficulty { get; set; } = "";
    public bool IsPlayable { get; set; }
    public string VoicingDescription { get; set; } = "";
    public List<string> Notes { get; set; } = [];
    public List<int> PitchClasses { get; set; } = [];
    public string? CagedShape { get; set; }
    public double? CagedSimilarity { get; set; }
    public string? FingeringPattern { get; set; }
    public BiomechanicalDataType? BiomechanicalData { get; set; }
    public PhysicalPlayabilityDataType? PhysicalPlayabilityData { get; set; }

    public static FretboardChordAnalysisType FromAnalysis(FretboardChordAnalyzer.FretboardChordAnalysis analysis,
        bool includePhysicalAnalysis = false)
    {
        PhysicalPlayabilityDataType? physicalData = null;
        if (includePhysicalAnalysis)
        {
            var physicalAnalysis = PhysicalFretboardCalculator.AnalyzePlayability(analysis.Positions);
            physicalData = PhysicalPlayabilityDataType.FromAnalysis(physicalAnalysis);
        }

        return new FretboardChordAnalysisType
        {
            ChordName = analysis.ChordName,
            HybridAnalysisName = analysis.HybridAnalysisName,
            IconicName = analysis.IconicName,
            IconicDescription = analysis.IconicDescription,
            FretSpan = analysis.FretSpan,
            LowestFret = analysis.LowestFret,
            HighestFret = analysis.HighestFret,
            Difficulty = analysis.Difficulty.ToString(),
            IsPlayable = analysis.IsPlayable,
            VoicingDescription = analysis.VoicingDescription,
            Notes = analysis.Notes.Select(n => n.ToString()).ToList(),
            PitchClasses = analysis.PitchClassSet.Select(pc => pc.Value).ToList(),
            CagedShape = analysis.CagedAnalysis?.ClosestShape?.ToString(),
            CagedSimilarity = analysis.CagedAnalysis?.Similarity,
            FingeringPattern = analysis.FingeringAnalysis?.Difficulty.ToString(),
            BiomechanicalData = analysis.BiomechanicalAnalysis != null
                ? BiomechanicalDataType.FromAnalysis(analysis.BiomechanicalAnalysis)
                : null,
            PhysicalPlayabilityData = physicalData
        };
    }
}

/// <summary>
///     GraphQL type for biomechanical analysis data
/// </summary>
public class BiomechanicalDataType
{
    public double Reachability { get; set; }
    public double Comfort { get; set; }
    public double Naturalness { get; set; }
    public double Efficiency { get; set; }
    public double Stability { get; set; }
    public double OverallScore { get; set; }
    public string Difficulty { get; set; } = "";
    public bool IsPlayable { get; set; }
    public string Reason { get; set; } = "";

    public static BiomechanicalDataType FromAnalysis(BiomechanicalPlayabilityAnalysis analysis)
    {
        return new BiomechanicalDataType
        {
            Reachability = analysis.Reachability,
            Comfort = analysis.Comfort,
            Naturalness = analysis.Naturalness,
            Efficiency = analysis.Efficiency,
            Stability = analysis.Stability,
            OverallScore = analysis.OverallScore,
            Difficulty = analysis.Difficulty,
            IsPlayable = analysis.IsPlayable,
            Reason = analysis.Reason
        };
    }
}

/// <summary>
///     GraphQL type for physical playability analysis data
/// </summary>
public class PhysicalPlayabilityDataType
{
    public double FretSpanMm { get; set; }
    public double MaxFingerStretchMm { get; set; }
    public double AverageFingerStretchMm { get; set; }
    public double VerticalSpanMm { get; set; }
    public double DiagonalStretchMm { get; set; }
    public string Difficulty { get; set; } = "";
    public bool IsPlayable { get; set; }
    public string DifficultyReason { get; set; } = "";
    public List<FingerPositionType> SuggestedFingering { get; set; } = [];

    public static PhysicalPlayabilityDataType FromAnalysis(
        PhysicalFretboardCalculator.PhysicalPlayabilityAnalysis analysis)
    {
        return new PhysicalPlayabilityDataType
        {
            FretSpanMm = analysis.FretSpanMm,
            MaxFingerStretchMm = analysis.MaxFingerStretchMm,
            AverageFingerStretchMm = analysis.AverageFingerStretchMm,
            VerticalSpanMm = analysis.VerticalSpanMm,
            DiagonalStretchMm = analysis.DiagonalStretchMm,
            Difficulty = analysis.Difficulty.ToString(),
            IsPlayable = analysis.IsPlayable,
            DifficultyReason = analysis.DifficultyReason,
            SuggestedFingering = analysis.SuggestedFingering
                .Select(FingerPositionType.FromFingerPosition)
                .ToList()
        };
    }
}

/// <summary>
///     GraphQL type for finger position data
/// </summary>
public class FingerPositionType
{
    public int String { get; set; }
    public int Fret { get; set; }
    public int FingerNumber { get; set; }
    public string Technique { get; set; } = "";

    public static FingerPositionType FromFingerPosition(PhysicalFretboardCalculator.FingerPosition fingerPosition)
    {
        return new FingerPositionType
        {
            String = fingerPosition.String.Value,
            Fret = fingerPosition.Fret.Value,
            FingerNumber = fingerPosition.FingerNumber,
            Technique = fingerPosition.Technique
        };
    }
}

/// <summary>
///     Input type for fret span queries
/// </summary>
public class FretSpanInput
{
    public int StartFret { get; set; } = 0;
    public int EndFret { get; set; } = 5;
    public int? MaxResults { get; set; }
    public string? DifficultyFilter { get; set; }
    public bool IncludeBiomechanicalAnalysis { get; set; } = false;
    public bool IncludePhysicalAnalysis { get; set; } = false;
}

/// <summary>
///     Result type for fret span analysis
/// </summary>
public class FretSpanAnalysisResult
{
    public int StartFret { get; set; }
    public int EndFret { get; set; }
    public int TotalChords { get; set; }
    public List<FretboardChordAnalysisType> Chords { get; set; } = [];
    public Dictionary<string, int> DifficultyDistribution { get; set; } = new();
    public Dictionary<string, int> CagedShapeDistribution { get; set; } = new();
}
