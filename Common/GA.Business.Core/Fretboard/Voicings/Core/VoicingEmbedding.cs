namespace GA.Business.Core.Fretboard.Voicings.Core;

using System;

/// <summary>
/// Voicing with embedding data for vector search
/// </summary>
public record VoicingEmbedding(
    string Id,
    string ChordName,
    string? VoicingType,
    string? Position,
    string? Difficulty,
    string? ModeName,
    string? ModalFamily,
    string[] PossibleKeys,
    string[] SemanticTags,
    string PrimeFormId,
    int TranslationOffset,
    string Diagram,
    int[] MidiNotes,
    string PitchClassSet,
    string IntervalClassVector,
    int MinFret,
    int MaxFret,
    bool BarreRequired,
    int HandStretch,
    string? StackingType, // Added for structured search
    int? RootPitchClass,   // Added for slash chord detection
    int MidiBassNote,     // Added for slash chord detection
    string? HarmonicFunction, // Added Phase 3
    bool IsNaturallyOccurring, // Added Phase 3
    double ConsonanceScore, // Added Phase 3
    double BrightnessScore, // Added Phase 3
    bool IsRootless, // Added Phase 3
    bool HasGuideTones, // Added Phase 3
    int Inversion, // Added Phase 3
    int? TopPitchClass, // Added for Chord Melody support
    string? TexturalDescription, // Added for AI Agents
    string[]? DoubledTones, // Added for AI Agents
    string[]? AlternateNames, // Added for AI Agents
    string[]? OmittedTones, // Added Phase 3
    string? CagedShape, // Added for CAGED support
    string Description,
    double[] Embedding,
    double[]? TextEmbedding); // Added Phase 7 for hybrid search

/// <summary>
/// Performance characteristics of a voicing search strategy
/// </summary>
public record VoicingSearchPerformance(
    TimeSpan ExpectedSearchTime,
    long MemoryUsageMb,
    bool RequiresGpu,
    bool RequiresNetwork);

/// <summary>
/// Voicing search statistics
/// </summary>
public record VoicingSearchStats(
    long TotalVoicings,
    long MemoryUsageMb,
    TimeSpan AverageSearchTime,
    long TotalSearches);

