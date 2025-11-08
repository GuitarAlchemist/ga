namespace GaApi.Models;

using System.Collections.Immutable;
using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Tonal;
using GA.Business.Core.Tonal.Modes;

/// <summary>
///     A chord analyzed within a specific musical context (key, scale, or mode)
/// </summary>
public record ChordInContext
{
    /// <summary>The chord template</summary>
    public required ChordTemplate Template { get; init; }

    /// <summary>The root pitch class of the chord</summary>
    public required PitchClass Root { get; init; }

    /// <summary>Contextual name (e.g., "Cmaj7" in C major, "Imaj7" as Roman numeral)</summary>
    public required string ContextualName { get; init; }

    /// <summary>Scale degree (1-7 for diatonic, null for non-diatonic)</summary>
    public int? ScaleDegree { get; init; }

    /// <summary>Harmonic function (Tonic, Subdominant, Dominant, etc.)</summary>
    public KeyAwareChordNamingService.ChordFunction Function { get; init; }

    /// <summary>Commonality score (0.0-1.0, how common in this context)</summary>
    public double Commonality { get; init; }

    /// <summary>True if the chord naturally occurs in the context</summary>
    public bool IsNaturallyOccurring { get; init; }

    /// <summary>Alternative names for this chord</summary>
    public IReadOnlyList<string> AlternateNames { get; init; } = [];

    /// <summary>Roman numeral representation (if applicable)</summary>
    public string? RomanNumeral { get; init; }

    /// <summary>Functional description (e.g., "Tonic function in C major")</summary>
    public string? FunctionalDescription { get; init; }

    /// <summary>The musical context (key, scale, or mode)</summary>
    public MusicalContext? Context { get; init; }

    /// <summary>Secondary dominant information (e.g., "V/V", "V7/ii")</summary>
    public SecondaryDominantInfo? SecondaryDominant { get; init; }

    /// <summary>True if this is a secondary dominant chord</summary>
    public bool IsSecondaryDominant => SecondaryDominant != null;

    /// <summary>Modulation information if this chord can pivot to another key</summary>
    public ModulationInfo? Modulation { get; init; }

    /// <summary>True if this chord is a central/hub chord in the harmonic graph (high betweenness centrality)</summary>
    public bool IsCentral { get; init; }

    /// <summary>True if this chord is an attractor in the dynamical system (stable harmonic destination)</summary>
    public bool IsAttractor { get; init; }

    /// <summary>Centrality score (0.0-1.0, how central this chord is in the harmonic network)</summary>
    public double Centrality { get; init; }

    /// <summary>Functional role based on dynamical systems analysis (Tonic Attractor, Dominant Attractor, Bridge, etc.)</summary>
    public string? DynamicalRole { get; init; }
}

/// <summary>
///     Musical context for chord analysis
/// </summary>
public record MusicalContext
{
    /// <summary>Context level (Key, Scale, Mode)</summary>
    public required ContextLevel Level { get; init; }

    /// <summary>Key context (if Level is Key)</summary>
    public Key? Key { get; init; }

    /// <summary>Scale context (if Level is Scale)</summary>
    public ScaleMode? Scale { get; init; }

    /// <summary>Mode context (if Level is Mode)</summary>
    public ScaleMode? Mode { get; init; }

    /// <summary>Display name for the context</summary>
    public string Name => Level switch
    {
        ContextLevel.Key => Key?.ToString() ?? "Unknown Key",
        ContextLevel.Scale => Scale?.Name ?? "Unknown Scale",
        ContextLevel.Mode => Mode?.Name ?? "Unknown Mode",
        _ => "No Context"
    };
}

/// <summary>
///     Context level for chord analysis
/// </summary>
public enum ContextLevel
{
    None,
    Key,
    Scale,
    Mode
}

/// <summary>
///     Filters for chord queries
/// </summary>
public record ChordFilters
{
    /// <summary>Maximum chord extension (Triad, Seventh, Ninth, etc.)</summary>
    public ChordExtension? Extension { get; init; }

    /// <summary>Stacking type (Tertian, Quartal, Quintal, etc.)</summary>
    public ChordStackingType? StackingType { get; init; }

    /// <summary>Maximum difficulty level</summary>
    public PlayabilityLevel? MaxDifficulty { get; init; }

    /// <summary>Include only naturally occurring chords</summary>
    public bool OnlyNaturallyOccurring { get; init; }

    /// <summary>Include borrowed chords (modal interchange)</summary>
    public bool IncludeBorrowedChords { get; init; } = true;

    /// <summary>Include secondary dominants (V/x, V7/x)</summary>
    public bool IncludeSecondaryDominants { get; init; } = true;

    /// <summary>Include secondary ii-V progressions</summary>
    public bool IncludeSecondaryTwoFive { get; init; } = true;

    /// <summary>Include modulation suggestions</summary>
    public bool IncludeModulations { get; init; } = false;

    /// <summary>Minimum commonality score (0.0-1.0)</summary>
    public double MinCommonality { get; init; } = 0.0;

    /// <summary>Maximum number of results</summary>
    public int Limit { get; init; } = 50;
}

/// <summary>
///     Chord extension level
/// </summary>
public enum ChordExtension
{
    Triad,
    Seventh,
    Ninth,
    Eleventh,
    Thirteenth
}

/// <summary>
///     Chord stacking type
/// </summary>
public enum ChordStackingType
{
    Tertian, // Stacked in thirds (most common)
    Quartal, // Stacked in fourths
    Quintal, // Stacked in fifths
    Secundal, // Stacked in seconds (clusters)
    Mixed // Mixed intervals
}

/// <summary>
///     A chord voicing with comprehensive analysis
/// </summary>
public record VoicingWithAnalysis
{
    /// <summary>The fretboard positions</summary>
    public required ImmutableList<Position> Positions { get; init; }

    /// <summary>Physical analysis (playability, difficulty, fret span)</summary>
    public required PhysicalAnalysis Physical { get; init; }

    /// <summary>Psychoacoustic analysis (consonance, brightness, etc.)</summary>
    public required PsychoacousticAnalysis Psychoacoustic { get; init; }

    /// <summary>CAGED shape (if applicable)</summary>
    public CagedShape? Shape { get; init; }

    /// <summary>Utility score (0.0-1.0, overall usefulness)</summary>
    public double UtilityScore { get; init; }

    /// <summary>Voice leading quality from previous chord (if applicable)</summary>
    public double? VoiceLeadingQuality { get; init; }

    /// <summary>Style tags (Jazz, Rock, Classical, etc.)</summary>
    public IReadOnlyList<string> StyleTags { get; init; } = [];
}

/// <summary>
///     Physical analysis of a voicing
/// </summary>
public record PhysicalAnalysis
{
    /// <summary>Playability level</summary>
    public required PlayabilityLevel Playability { get; init; }

    /// <summary>Fret span (number of frets covered)</summary>
    public required int FretSpan { get; init; }

    /// <summary>Lowest fret used</summary>
    public required int LowestFret { get; init; }

    /// <summary>Highest fret used</summary>
    public required int HighestFret { get; init; }

    /// <summary>Finger stretch required (0.0-1.0)</summary>
    public required double FingerStretch { get; init; }

    /// <summary>Requires barre</summary>
    public required bool RequiresBarre { get; init; }

    /// <summary>Has open strings</summary>
    public required bool HasOpenStrings { get; init; }

    /// <summary>Has muted strings</summary>
    public required bool HasMutedStrings { get; init; }

    /// <summary>Number of strings played</summary>
    public required int StringCount { get; init; }
}

/// <summary>
///     Psychoacoustic analysis of a voicing
/// </summary>
public record PsychoacousticAnalysis
{
    /// <summary>Consonance score (0.0-1.0, higher is more consonant)</summary>
    public required double Consonance { get; init; }

    /// <summary>Brightness score (0.0-1.0, higher is brighter)</summary>
    public required double Brightness { get; init; }

    /// <summary>Clarity score (0.0-1.0, higher is clearer)</summary>
    public required double Clarity { get; init; }

    /// <summary>Harmonic strength (0.0-1.0)</summary>
    public required double HarmonicStrength { get; init; }

    /// <summary>Register (Low, Medium, High)</summary>
    public required string Register { get; init; }

    /// <summary>Density (Sparse, Medium, Dense)</summary>
    public required string Density { get; init; }
}

/// <summary>
///     Playability level
/// </summary>
public enum PlayabilityLevel
{
    Beginner,
    Intermediate,
    Advanced,
    Expert
}

/// <summary>
///     CAGED system shapes
/// </summary>
public enum CagedShape
{
    C,
    A,
    G,
    E,
    D
}

/// <summary>
///     Filters for voicing queries
/// </summary>
public record VoicingFilters
{
    /// <summary>Maximum difficulty level</summary>
    public PlayabilityLevel? MaxDifficulty { get; init; }

    /// <summary>Fret range (min, max)</summary>
    public FretRange? FretRange { get; init; }

    /// <summary>CAGED shape filter</summary>
    public CagedShape? CagedShape { get; init; }

    /// <summary>Exclude voicings with open strings</summary>
    public bool NoOpenStrings { get; init; }

    /// <summary>Exclude voicings with muted strings</summary>
    public bool NoMutedStrings { get; init; }

    /// <summary>Exclude voicings requiring barre</summary>
    public bool NoBarres { get; init; }

    /// <summary>Minimum consonance score (0.0-1.0)</summary>
    public double MinConsonance { get; init; } = 0.0;

    /// <summary>Style preference (Jazz, Rock, Classical, etc.)</summary>
    public string? StylePreference { get; init; }

    /// <summary>Maximum number of results</summary>
    public int Limit { get; init; } = 20;
}

/// <summary>
///     Information about a secondary dominant chord
/// </summary>
public record SecondaryDominantInfo
{
    /// <summary>Target scale degree (e.g., 5 for V/V)</summary>
    public required int TargetDegree { get; init; }

    /// <summary>Target chord name (e.g., "G" for V/V in C major)</summary>
    public required string TargetChordName { get; init; }

    /// <summary>Roman numeral notation (e.g., "V/V", "V7/ii")</summary>
    public required string Notation { get; init; }

    /// <summary>Description (e.g., "Secondary dominant to the dominant")</summary>
    public required string Description { get; init; }

    /// <summary>True if this is part of a ii-V progression (e.g., ii/V-V/V)</summary>
    public bool IsPartOfTwoFive { get; init; }
}

/// <summary>
///     Information about modulation possibilities
/// </summary>
public record ModulationInfo
{
    /// <summary>Target key for modulation</summary>
    public required Key TargetKey { get; init; }

    /// <summary>Modulation type (Relative, Parallel, Dominant, etc.)</summary>
    public required ModulationType Type { get; init; }

    /// <summary>True if this chord is a pivot chord (exists in both keys)</summary>
    public bool IsPivotChord { get; init; }

    /// <summary>Roman numeral in original key</summary>
    public string? RomanNumeralInOriginalKey { get; init; }

    /// <summary>Roman numeral in target key</summary>
    public string? RomanNumeralInTargetKey { get; init; }

    /// <summary>Description of the modulation</summary>
    public string? Description { get; init; }
}

/// <summary>
///     Types of key modulation
/// </summary>
public enum ModulationType
{
    /// <summary>To relative major/minor (e.g., C major to A minor)</summary>
    Relative,

    /// <summary>To parallel major/minor (e.g., C major to C minor)</summary>
    Parallel,

    /// <summary>To dominant key (e.g., C major to G major)</summary>
    Dominant,

    /// <summary>To subdominant key (e.g., C major to F major)</summary>
    Subdominant,

    /// <summary>To supertonic key (e.g., C major to D minor)</summary>
    Supertonic,

    /// <summary>To mediant key (e.g., C major to E minor)</summary>
    Mediant,

    /// <summary>To submediant key (e.g., C major to A minor)</summary>
    Submediant,

    /// <summary>Chromatic modulation (distant key)</summary>
    Chromatic
}

/// <summary>
///     Fret range for filtering
/// </summary>
public record FretRange(int Min, int Max);
