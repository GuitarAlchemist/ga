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
    string? CagedShape = null,        // "C", "A", "G", "E", "D"

    // Structured Query filters (Requested)
    string? ChordName = null,         // Partial or exact match
    string? StackingType = null,      // "Tertian", "Quartal", "Secundal"
    bool? IsSlashChord = null,        // Bass note != Root
    int? MinMidiPitch = null,         // Lowest note in voicing
    int? MaxMidiPitch = null,         // Highest note in voicing
    int? FingerCount = null,          // Number of fingers used (approx)
    string? SetClassId = null,        // e.g., "3-11"
    string? RahnPrimeForm = null,     // e.g., "{0,3,7}"

    // Extended Filters (Phase 3)
    string? HarmonicFunction = null,  // "Tonic", "Dominant", etc.
    bool? IsNaturallyOccurring = null,
    bool? HasGuideTones = null,
    int? Inversion = null,            // 0=Root, 1=1st, etc.
    double? MinConsonance = null,     // 0.0-1.0
    double? MinBrightness = null,     // 0.0-1.0
    double? MaxBrightness = null,     // 0.0-1.0
    string[]? OmittedTones = null,    // e.g. ["Root", "5th"]
    int? TopPitchClass = null,        // 0-11 for Melody Note
    
    // AI Agent Metadata Filters (Phase 4)
    string? TexturalDescriptionContains = null, // e.g., "warm", "muddy"
    string[]? DoubledTonesContain = null,       // e.g., ["5th"]
    string? AlternateNameMatch = null,           // Match C6/Am7 equivalents
    int[]? SymbolicBitIndices = null             // Indices 0-11 for targeted trait boosting
);

