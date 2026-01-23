namespace GA.Domain.Core.Common;

/// <summary>
/// Centralized constants for the Guitar Alchemist domain to avoid magic strings.
/// </summary>
public static class AnalysisConstants
{
    public const string DefaultTuning = "Standard";
    public const string TabAnalysisEngine = "TabParser + VoicingHarmonicAnalyzer";
    public const string DomainAnalysisEngine = "GuitarAlchemist.VoicingAnalyzer";
    public const string FunctionalHarmony = "Functional Harmony";
    public const string FunctionalDescriptionUnknown = "Atonal";
    public const string Unknown = "Unknown";
    public const string UnknownQuality = "Unknown Quality";
}
