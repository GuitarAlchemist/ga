namespace GA.Business.Core.Fretboard.Voicings.Analysis;

using System.Collections.Generic;
using GA.Business.Core.Notes.Primitives;
using GA.Business.Core.Atonal.Primitives;
using Atonal; // For PitchClass
using Tonal;

/// <summary>
/// Comprehensive musical analysis result for a voicing (3-layer architecture)
/// </summary>
public record MusicalVoicingAnalysis(
    // Core
    MidiNote[] MidiNotes,
    PitchClassSet PitchClassSet,
    
    // Layer 1: Identity
    ChordIdentification ChordId,
    List<string>? AlternateChordNames,
    bool RequiresContext,
    ModeInfo? ModeInfo,
    EquivalenceInfo? EquivalenceInfo,
    
    // Layer 2: Sound
    VoicingCharacteristics VoicingCharacteristics,
    ToneInventory ToneInventory,
    PerceptualQualities PerceptualQualities,
    SymmetricalScaleInfo? SymmetricalInfo,
    IntervallicInfo IntervallicInfo,
    List<string>? ChromaticNotes,
    
    // Layer 3: Hands
    PhysicalLayout PhysicalLayout,
    PlayabilityInfo PlayabilityInfo,
    ErgonomicsInfo ErgonomicsInfo,
    
    // Contextual
    List<string> SemanticTags,
    ContextualHooks ContextualHooks
);

/// <summary>
/// Chord identification information with confidence and root tracking
/// </summary>
public record ChordIdentification(
    string? ChordName,
    string? AlternateName,
    Key? ClosestKey,
    string? RomanNumeral,
    string? FunctionalDescription,
    string? HarmonicFunction,
    SlashChordInfo? SlashChordInfo,
    bool IsNaturallyOccurring,
    ChordIntervals? Intervals,
    PitchClass? RootPitchClass,
    double RootConfidence
);

/// <summary>
/// Slash chord information
/// </summary>
public record SlashChordInfo(
    string Notation,
    string Type,
    bool IsCommonInversion
);

/// <summary>
/// Voicing characteristics (open/closed, drop voicings, etc.)
/// </summary>
public record VoicingCharacteristics(
    bool IsOpenVoicing,
    bool IsRootless,
    string? DropVoicing,
    string? VoicingType,
    int Span,
    List<string> Features
);

/// <summary>
/// Tone inventory: which chord tones are present, doubled, or omitted
/// </summary>
public record ToneInventory(
    List<string> TonesPresent,
    List<string> DoubledTones,
    List<string> OmittedTones,
    bool HasGuideTones,
    bool HasRoot,
    bool HasThird,
    bool HasFifth,
    bool HasSeventh
);

/// <summary>
/// Perceptual and psychoacoustic qualities (real computed values, not placeholders)
/// </summary>
public record PerceptualQualities(
    /// <summary>Register classification (Low, Mid-Low, Mid, Mid-High, High)</summary>
    string Register,
    /// <summary>Brightness proxy 0.0-1.0 (spectral centroid approximation)</summary>
    double Brightness,
    /// <summary>Consonance score 0.0-1.0 (inverse of roughness + mud penalty)</summary>
    double ConsonanceScore,
    /// <summary>Roughness proxy 0.0-1.0 (psychoacoustic beating estimate)</summary>
    double Roughness,
    /// <summary>Spacing classification: Close, Mixed, or Open</summary>
    string Spacing,
    /// <summary>True if low-interval mud penalty detected (close intervals in bass)</summary>
    bool MayBeMuddy,
    /// <summary>Human-readable textural description</summary>
    string? TexturalDescription
);

/// <summary>
/// Mode information
/// </summary>
public record ModeInfo(
    string ModeName,
    int NoteCount,
    int DegreeInFamily,
    string? FamilyName = null
);

/// <summary>
/// Chord intervals (theoretical and actual)
/// </summary>
public record ChordIntervals(
    List<string> Theoretical,
    List<string> Actual
);

/// <summary>
/// Symmetrical scale information
/// </summary>
public record SymmetricalScaleInfo(
    string ScaleName,
    List<string> PossibleRoots,
    string Description
);

/// <summary>
/// Intervallic content information
/// </summary>
public record IntervallicInfo(
    string IntervalClassVector,
    List<string> Features
);

/// <summary>
/// Equivalence group information for pattern recognition
/// </summary>
public record EquivalenceInfo(
    string PrimeFormId,
    string? ForteCode,
    bool IsPrimeForm,
    int TranslationOffset,
    int EquivalenceClassSize
);

/// <summary>
/// Physical layout on the fretboard
/// </summary>
public record PhysicalLayout(
    int[] FretPositions,
    int[] StringsUsed,
    int[] MutedStrings,
    int[] OpenStrings,
    int MinFret,
    int MaxFret,
    string HandPosition,
    string? StringSet
);

/// <summary>
/// Playability and difficulty information
/// </summary>
public record PlayabilityInfo(
    string Difficulty,
    int HandStretch,
    bool BarreRequired,
    string? BarreInfo,
    int MinimumFingers,
    string? CagedShape,
    string? ShellFamily,
    double DifficultyScore
);

/// <summary>
/// Detailed ergonomics information for guitar-specific analysis
/// </summary>
public record ErgonomicsInfo(
    int StringSkips,
    string? FingerAssignment,
    bool RequiresThumb,
    bool IsPhysicallyImpossible,
    string? ErgonomicNotes
);

/// <summary>
/// Contextual hooks for musical use
/// </summary>
public record ContextualHooks(
    List<string>? CommonSubstitutions,
    List<string>? PlayStyles,
    List<string>? GenreTags,
    List<string>? SongReferences
);
