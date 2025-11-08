namespace GaApi.Models;

using Services;

/// <summary>
///     DTO for a chord analyzed within a specific musical context
/// </summary>
public record ChordInContextDto
{
    /// <summary>The chord template name</summary>
    public required string TemplateName { get; init; }

    /// <summary>The root pitch class name (e.g., "C", "D#")</summary>
    public required string Root { get; init; }

    /// <summary>Contextual name (e.g., "Cmaj7" in C major, "Imaj7" as Roman numeral)</summary>
    public required string ContextualName { get; init; }

    /// <summary>Scale degree (1-7 for diatonic, null for non-diatonic)</summary>
    public int? ScaleDegree { get; init; }

    /// <summary>Harmonic function (Tonic, Subdominant, Dominant, etc.)</summary>
    public string Function { get; init; } = "Unknown";

    /// <summary>Commonality score (0.0-1.0, how common in this context)</summary>
    public double Commonality { get; init; }

    /// <summary>True if the chord naturally occurs in the context</summary>
    public bool IsNaturallyOccurring { get; init; }

    /// <summary>Alternative names for this chord</summary>
    public List<string> AlternateNames { get; init; } = [];

    /// <summary>Roman numeral representation (if applicable)</summary>
    public string? RomanNumeral { get; init; }

    /// <summary>Functional description (e.g., "Tonic function in C major")</summary>
    public string? FunctionalDescription { get; init; }

    /// <summary>The musical context (key, scale, or mode)</summary>
    public MusicalContextDto? Context { get; init; }

    /// <summary>Secondary dominant information (e.g., "V/V", "V7/ii")</summary>
    public SecondaryDominantInfoDto? SecondaryDominant { get; init; }

    /// <summary>True if this is a secondary dominant chord</summary>
    public bool IsSecondaryDominant => SecondaryDominant != null;

    /// <summary>Modulation information if this chord can pivot to another key</summary>
    public ModulationInfoDto? Modulation { get; init; }

    /// <summary>Interval structure (e.g., "1, 3, 5, 7")</summary>
    public List<int> Intervals { get; init; } = [];

    /// <summary>True if this chord is a central/hub chord in the harmonic graph</summary>
    public bool IsCentral { get; init; }

    /// <summary>True if this chord is an attractor in the dynamical system</summary>
    public bool IsAttractor { get; init; }

    /// <summary>Centrality score (0.0-1.0)</summary>
    public double Centrality { get; init; }

    /// <summary>Dynamical role (Tonic Attractor, Dominant Attractor, Bridge, etc.)</summary>
    public string? DynamicalRole { get; init; }
}

/// <summary>
///     DTO for musical context
/// </summary>
public record MusicalContextDto
{
    /// <summary>Context level (Key, Scale, Mode)</summary>
    public required string Level { get; init; }

    /// <summary>Display name for the context</summary>
    public required string Name { get; init; }
}

/// <summary>
///     DTO for secondary dominant information
/// </summary>
public record SecondaryDominantInfoDto
{
    /// <summary>Target scale degree (e.g., 5 for V/V)</summary>
    public int TargetDegree { get; init; }

    /// <summary>Target chord name (e.g., "G" for V/V in C major)</summary>
    public string TargetChordName { get; init; } = string.Empty;

    /// <summary>Notation (e.g., "V/V", "V7/ii", "ii/V")</summary>
    public string Notation { get; init; } = string.Empty;

    /// <summary>Description</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>True if this is part of a ii-V progression</summary>
    public bool IsPartOfTwoFive { get; init; }
}

/// <summary>
///     DTO for modulation information
/// </summary>
public record ModulationInfoDto
{
    /// <summary>Target key</summary>
    public required string TargetKey { get; init; }

    /// <summary>Modulation type</summary>
    public required string ModulationType { get; init; }

    /// <summary>Pivot chords (chords that exist in both keys)</summary>
    public List<string> PivotChords { get; init; } = [];

    /// <summary>Description</summary>
    public string Description { get; init; } = string.Empty;
}

/// <summary>
///     DTO for modulation suggestion
/// </summary>
public record ModulationSuggestionDto
{
    /// <summary>Source key</summary>
    public required string SourceKey { get; init; }

    /// <summary>Target key</summary>
    public required string TargetKey { get; init; }

    /// <summary>Modulation type</summary>
    public required string ModulationType { get; init; }

    /// <summary>Pivot chords</summary>
    public required List<PivotChordDto> PivotChords { get; init; }

    /// <summary>Description</summary>
    public required string Description { get; init; }

    /// <summary>Difficulty (0.0 = easy, 1.0 = difficult)</summary>
    public double Difficulty { get; init; }

    /// <summary>Suggested progression</summary>
    public List<string> SuggestedProgression { get; init; } = [];
}

/// <summary>
///     DTO for pivot chord
/// </summary>
public record PivotChordDto
{
    /// <summary>Chord name</summary>
    public required string ChordName { get; init; }

    /// <summary>Degree in source key</summary>
    public int DegreeInSourceKey { get; init; }

    /// <summary>Degree in target key</summary>
    public int DegreeInTargetKey { get; init; }

    /// <summary>Roman numeral in source key</summary>
    public required string RomanNumeralInSourceKey { get; init; }

    /// <summary>Roman numeral in target key</summary>
    public required string RomanNumeralInTargetKey { get; init; }

    /// <summary>Function description</summary>
    public required string Function { get; init; }
}

/// <summary>
///     DTO for voicing with analysis
/// </summary>
public record VoicingWithAnalysisDto
{
    /// <summary>Chord name</summary>
    public required string ChordName { get; init; }

    /// <summary>Fret positions for each string (0 = open, -1 = muted)</summary>
    public required List<int> Positions { get; init; }

    /// <summary>Physical analysis</summary>
    public required PhysicalAnalysisDto Physical { get; init; }

    /// <summary>Psychoacoustic analysis</summary>
    public required PsychoacousticAnalysisDto Psychoacoustic { get; init; }

    /// <summary>Difficulty level</summary>
    public string Difficulty { get; init; } = "Unknown";

    /// <summary>Style tags</summary>
    public List<string> StyleTags { get; init; } = [];

    /// <summary>CAGED shape (if applicable)</summary>
    public string? CagedShape { get; init; }

    /// <summary>Utility score (0.0-1.0)</summary>
    public double UtilityScore { get; init; }
}

/// <summary>
///     DTO for physical analysis
/// </summary>
public record PhysicalAnalysisDto
{
    /// <summary>Fret span (highest - lowest fret)</summary>
    public int FretSpan { get; init; }

    /// <summary>Finger stretch (max distance between adjacent fingers)</summary>
    public int FingerStretch { get; init; }

    /// <summary>Lowest fret used</summary>
    public int LowestFret { get; init; }

    /// <summary>Highest fret used</summary>
    public int HighestFret { get; init; }

    /// <summary>Number of strings used</summary>
    public int StringsUsed { get; init; }

    /// <summary>Number of muted strings</summary>
    public int MutedStrings { get; init; }

    /// <summary>Number of open strings</summary>
    public int OpenStrings { get; init; }

    /// <summary>Number of barred strings (if any)</summary>
    public int BarredStrings { get; init; }
}

/// <summary>
///     DTO for psychoacoustic analysis
/// </summary>
public record PsychoacousticAnalysisDto
{
    /// <summary>Consonance score (0.0-1.0, higher = more consonant)</summary>
    public double Consonance { get; init; }

    /// <summary>Brightness score (0.0-1.0, higher = brighter)</summary>
    public double Brightness { get; init; }

    /// <summary>Tension score (0.0-1.0, higher = more tension)</summary>
    public double Tension { get; init; }

    /// <summary>Density score (0.0-1.0, higher = more notes)</summary>
    public double Density { get; init; }
}

/// <summary>
///     Mapper to convert domain models to DTOs
/// </summary>
public static class ContextualChordMapper
{
    public static ChordInContextDto ToDto(ChordInContext chord)
    {
        return new ChordInContextDto
        {
            TemplateName = chord.Template.Name,
            Root = chord.Root.ToString(),
            ContextualName = chord.ContextualName,
            ScaleDegree = chord.ScaleDegree,
            Function = chord.Function.ToString(),
            Commonality = chord.Commonality,
            IsNaturallyOccurring = chord.IsNaturallyOccurring,
            AlternateNames = chord.AlternateNames.ToList(),
            RomanNumeral = chord.RomanNumeral,
            FunctionalDescription = chord.FunctionalDescription,
            Context = chord.Context != null
                ? new MusicalContextDto
                {
                    Level = chord.Context.Level.ToString(),
                    Name = chord.Context.Name
                }
                : null,
            SecondaryDominant = chord.SecondaryDominant != null
                ? new SecondaryDominantInfoDto
                {
                    TargetDegree = chord.SecondaryDominant.TargetDegree,
                    TargetChordName = chord.SecondaryDominant.TargetChordName,
                    Notation = chord.SecondaryDominant.Notation,
                    Description = chord.SecondaryDominant.Description,
                    IsPartOfTwoFive = chord.SecondaryDominant.IsPartOfTwoFive
                }
                : null,
            Modulation = chord.Modulation != null
                ? new ModulationInfoDto
                {
                    TargetKey = chord.Modulation.TargetKey.ToString(),
                    ModulationType = "Unknown", // TODO: Add when modulation is fully implemented
                    PivotChords = [], // TODO: Add when modulation is fully implemented
                    Description = "Modulation available" // TODO: Add when modulation is fully implemented
                }
                : null,
            Intervals = chord.Template.Intervals.Select(i => i.Interval.Semitones.Value).ToList(),
            IsCentral = chord.IsCentral,
            IsAttractor = chord.IsAttractor,
            Centrality = chord.Centrality,
            DynamicalRole = chord.DynamicalRole
        };
    }

    public static VoicingWithAnalysisDto ToDto(VoicingWithAnalysis voicing)
    {
        return new VoicingWithAnalysisDto
        {
            ChordName = "Unknown", // Will be set by caller
            Positions = voicing.Positions.Select(p => p.Location.Fret.Value).ToList(),
            Physical = new PhysicalAnalysisDto
            {
                FretSpan = voicing.Physical.FretSpan,
                FingerStretch = (int)(voicing.Physical.FingerStretch * 10), // Convert 0.0-1.0 to frets
                LowestFret = voicing.Physical.LowestFret,
                HighestFret = voicing.Physical.HighestFret,
                StringsUsed = voicing.Physical.StringCount,
                MutedStrings = voicing.Physical.HasMutedStrings ? 1 : 0, // Simplified
                OpenStrings = voicing.Physical.HasOpenStrings ? 1 : 0, // Simplified
                BarredStrings = voicing.Physical.RequiresBarre ? 1 : 0 // Simplified
            },
            Psychoacoustic = new PsychoacousticAnalysisDto
            {
                Consonance = voicing.Psychoacoustic.Consonance,
                Brightness = voicing.Psychoacoustic.Brightness,
                Tension = 1.0 - voicing.Psychoacoustic.Consonance, // Inverse of consonance
                Density = voicing.Psychoacoustic.Density == "Dense" ? 1.0 :
                    voicing.Psychoacoustic.Density == "Medium" ? 0.5 : 0.0
            },
            Difficulty = voicing.Physical.Playability.ToString(),
            StyleTags = voicing.StyleTags.ToList(),
            CagedShape = voicing.Shape?.ToString(),
            UtilityScore = voicing.UtilityScore
        };
    }

    public static ModulationSuggestionDto ToDto(ModulationSuggestion suggestion)
    {
        return new ModulationSuggestionDto
        {
            SourceKey = suggestion.SourceKey.ToString(),
            TargetKey = suggestion.TargetKey.ToString(),
            ModulationType = suggestion.Type.ToString(),
            PivotChords = suggestion.PivotChords.Select(ToDto).ToList(),
            Description = suggestion.Description,
            Difficulty = suggestion.Difficulty,
            SuggestedProgression = suggestion.SuggestedProgression?.ToList() ?? []
        };
    }

    public static PivotChordDto ToDto(PivotChord pivotChord)
    {
        return new PivotChordDto
        {
            ChordName = pivotChord.ChordName,
            DegreeInSourceKey = pivotChord.DegreeInSourceKey,
            DegreeInTargetKey = pivotChord.DegreeInTargetKey,
            RomanNumeralInSourceKey = pivotChord.RomanNumeralInSourceKey,
            RomanNumeralInTargetKey = pivotChord.RomanNumeralInTargetKey,
            Function = pivotChord.Function
        };
    }
}
