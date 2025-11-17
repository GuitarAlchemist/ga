namespace GA.Business.Core.Fretboard.Voicings.Search;

using Biomechanics;

/// <summary>
/// Search result containing a voicing document and its similarity score
/// </summary>
public record VoicingSearchResult(
    VoicingDocument Document,
    double Score,
    string Query);

/// <summary>
/// Filters for voicing search
/// </summary>
public record VoicingSearchFilters(
    // Basic filters
    string? Difficulty = null,
    string? Position = null,
    string? VoicingType = null,
    string? ModeName = null,
    string[]? Tags = null,
    int? MinFret = null,
    int? MaxFret = null,
    bool? RequireBarreChord = null,

    // Biomechanical filters (Quick Win 1)
    HandSize? HandSize = null,
    double? MaxFingerStretch = null,  // Maximum fret span (e.g., 3.0 for small hands)
    double? MinComfortScore = null,   // Minimum comfort score 0-1
    bool? MustBeErgonomic = null,     // Require ergonomic wrist posture

    // Musical characteristic filters (Quick Win 3)
    bool? IsOpenVoicing = null,       // Open vs closed voicing
    bool? IsRootless = null,          // Rootless voicing
    string? DropVoicing = null,       // "Drop-2", "Drop-3", etc.
    string? CagedShape = null         // "C", "A", "G", "E", "D"
);

