namespace GaApi.Models;

/// <summary>
/// Represents a chord within a specific musical context (e.g. a key or scale degree).
/// Matches frontend ChordInContext interface.
/// </summary>
public record ChordInContext(
    string TemplateName,
    string Root,
    string ContextualName,
    int? ScaleDegree,
    string Function,
    double Commonality,
    bool IsNaturallyOccurring,
    string[] AlternateNames,
    string[] Notes,
    string? RomanNumeral = null,
    string? FunctionalDescription = null);

/// <summary>
/// A chord borrowed via modal interchange from a parallel mode of the home key.
/// </summary>
public record BorrowedChordInContext(
    string TemplateName,
    string Root,
    string ContextualName,
    int? ScaleDegree,
    string Function,
    double Commonality,
    bool IsNaturallyOccurring,
    string[] AlternateNames,
    string[] Notes,
    string SourceMode,
    string? RomanNumeral = null,
    string? FunctionalDescription = null);

/// <summary>
/// Represents a guitar voicing with its physical and musical analysis.
/// Matches frontend VoicingWithAnalysis interface.
/// </summary>
public record VoicingWithAnalysis(
    string ChordName,
    int[] Frets,
    string Difficulty,
    double DifficultyScore,
    string HandPosition,
    string StringSet,
    string? CagedShape,
    string[] SemanticTags,
    bool IsBarreChord,
    string? BarreInfo);
