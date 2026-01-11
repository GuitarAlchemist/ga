namespace GA.Business.Core.Fretboard.Biomechanics;

/// <summary>
///     Comprehensive biomechanical playability analysis for a chord fingering
/// </summary>
public record BiomechanicalPlayabilityAnalysis(
    double OverallScore,
    string Difficulty,
    bool IsPlayable,
    double Reachability,
    double Comfort,
    double Naturalness,
    double Efficiency,
    double Stability,
    StretchAnalysis? StretchAnalysis,
    FingeringEfficiencyAnalysis? FingeringEfficiencyAnalysis,
    WristPostureAnalysis? WristPostureAnalysis,
    MutingAnalysis? MutingAnalysis,
    SlideLegatoAnalysis? SlideLegatoAnalysis,
    HandPose? BestPose
);

/// <summary>
///     Analysis of finger stretch requirements
/// </summary>
public record StretchAnalysis(
    double MaxStretchDistance,
    int MaxFretSpan,
    string StretchDescription,
    bool HasWideStretches,
    int WideStretchCount
);

/// <summary>
///     Analysis of wrist posture
/// </summary>
public record WristPostureAnalysis(
    double WristAngleDegrees,
    PostureType PostureType,
    bool IsErgonomic
);

/// <summary>
///     Wrist posture types
/// </summary>
public enum PostureType
{
    Neutral,
    SlightlyFlexed,
    Flexed,
    SlightlyExtended,
    Extended
}

/// <summary>
///     Analysis of muting technique
/// </summary>
public record MutingAnalysis(
    MutingTechnique Technique,
    string Reason
);

/// <summary>
///     Muting techniques
/// </summary>
public enum MutingTechnique
{
    None,
    PalmMuting,
    FingerMuting,
    ThumbMuting
}

/// <summary>
///     Analysis of slide/legato technique
/// </summary>
public record SlideLegatoAnalysis(
    SlideLegatoTechnique Technique,
    string Reason
);

/// <summary>
///     Slide/legato techniques
/// </summary>
public enum SlideLegatoTechnique
{
    None,
    Slide,
    HammerOn,
    PullOff,
    Legato
}
