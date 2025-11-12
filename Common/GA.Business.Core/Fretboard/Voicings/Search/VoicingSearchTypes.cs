namespace GA.Business.Core.Fretboard.Voicings.Search;

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
    string? Difficulty = null,
    string? Position = null,
    string? VoicingType = null,
    string? ModeName = null,
    string[]? Tags = null,
    int? MinFret = null,
    int? MaxFret = null,
    bool? RequireBarreChord = null);

