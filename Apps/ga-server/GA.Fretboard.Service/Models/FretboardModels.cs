namespace GA.Fretboard.Service.Models;

/// <summary>
/// Chord in context DTO
/// </summary>
public class ChordInContextDto
{
    public string Id { get; set; } = string.Empty;
    public string ChordName { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<string> Notes { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Additional properties needed by controllers
    public string Name { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Root { get; set; } = string.Empty;
}

/// <summary>
/// Voicing with analysis DTO
/// </summary>
public class VoicingWithAnalysisDto
{
    public string Id { get; set; } = string.Empty;
    public string VoicingName { get; set; } = string.Empty;
    public List<string> Notes { get; set; } = new();
    public Dictionary<string, double> AnalysisData { get; set; } = new();
    public PlayabilityLevel Playability { get; set; }
    public CagedShape CagedShape { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    // Additional properties needed by controllers
    public string ChordName { get; set; } = string.Empty;
    public List<FretPosition> Positions { get; set; } = new();
    public string Fingering { get; set; } = string.Empty;
    public VoicingAnalysisDto Analysis { get; set; } = new();
}

/// <summary>
/// Modulation suggestion DTO
/// </summary>
public class ModulationSuggestionDto
{
    public string Id { get; set; } = string.Empty;
    public string FromKey { get; set; } = string.Empty;
    public string ToKey { get; set; } = string.Empty;
    public string ModulationType { get; set; } = string.Empty;
    public List<string> TransitionChords { get; set; } = new();
    public double Confidence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Chord extension enum
/// </summary>
public enum ChordExtension
{
    None,
    Seventh,
    Ninth,
    Eleventh,
    Thirteenth,
    Add9,
    Add11,
    Sus2,
    Sus4
}

/// <summary>
/// Chord stacking type enum
/// </summary>
public enum ChordStackingType
{
    Tertian,
    Quartal,
    Quintal,
    Secundal,
    Mixed
}

/// <summary>
/// Playability level enum
/// </summary>
public enum PlayabilityLevel
{
    Easy,
    Moderate,
    Difficult,
    Expert
}

/// <summary>
/// CAGED shape enum
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
/// Chord statistics
/// </summary>
public class ChordStatistics
{
    public int TotalChords { get; set; }
    public int UniqueQualities { get; set; }
    public int UniqueRoots { get; set; }
    public string MostCommonQuality { get; set; } = string.Empty;
    public string MostCommonRoot { get; set; } = string.Empty;
    public Dictionary<string, int> QualityDistribution { get; set; } = new();
    public Dictionary<string, int> RootDistribution { get; set; } = new();
    public double AverageNotesPerChord { get; set; }
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Chord error
/// </summary>
public class ChordError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public ChordErrorType Type { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Music room document
/// </summary>
public class MusicRoomDocument
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object> Configuration { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Room generation job
/// </summary>
public class RoomGenerationJob
{
    public string Id { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Dynamical system info
/// </summary>
public class DynamicalSystemInfo
{
    public string Id { get; set; } = string.Empty;
    public string SystemType { get; set; } = string.Empty;
    public Dictionary<string, double> Parameters { get; set; } = new();
    public Dictionary<string, object> StateData { get; set; } = new();
    public double LyapunovExponent { get; set; }
    public List<object> Attractors { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Additional properties needed by controllers
    public bool IsStable { get; set; }
    public double Entropy { get; set; }
    public double Complexity { get; set; }
    public double Predictability { get; set; }
}

/// <summary>
/// Guitar agent response
/// </summary>
public class GuitarAgentResponse
{
    public string Id { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Chord filters
/// </summary>
public class ChordFilters
{
    public string? Quality { get; set; }
    public string? Root { get; set; }
    public ChordExtension? Extension { get; set; }
    public ChordStackingType? StackingType { get; set; }
    public int? MinNotes { get; set; }
    public int? MaxNotes { get; set; }
    public bool OnlyNaturallyOccurring { get; set; }
    public bool IncludeBorrowedChords { get; set; }
    public bool IncludeSecondaryDominants { get; set; }
    public bool IncludeSecondaryTwoFive { get; set; }
    public double MinCommonality { get; set; }
    public int Limit { get; set; } = 10;
}

/// <summary>
/// Voicing filters
/// </summary>
public class VoicingFilters
{
    public PlayabilityLevel? MaxDifficulty { get; set; }
    public CagedShape? PreferredShape { get; set; }
    public CagedShape? CagedShape { get; set; }
    public int? MinFret { get; set; }
    public int? MaxFret { get; set; }
    public FretRange? FretRange { get; set; }
    public bool NoOpenStrings { get; set; }
    public bool NoMutedStrings { get; set; }
    public bool NoBarres { get; set; }
    public double MinConsonance { get; set; }
    public string? StylePreference { get; set; }
    public int Limit { get; set; } = 10;
}

/// <summary>
/// Fret range
/// </summary>
public class FretRange
{
    public int MinFret { get; set; }
    public int MaxFret { get; set; }

    public FretRange() { }

    public FretRange(int minFret, int maxFret)
    {
        MinFret = minFret;
        MaxFret = maxFret;
    }
}

/// <summary>
/// Neck config
/// </summary>
public class NeckConfig
{
    public string Tuning { get; set; } = "Standard";
    public int Frets { get; set; } = 24;
    public double ScaleLength { get; set; } = 25.5;
}

/// <summary>
/// Sound sample
/// </summary>
public class SoundSample
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public byte[] AudioData { get; set; } = Array.Empty<byte>();
    public TimeSpan Duration { get; set; }
    public string Format { get; set; } = "wav";
}

/// <summary>
/// Contextual chord mapper
/// </summary>
public static class ContextualChordMapper
{
    public static object MapToDto(object chord)
    {
        return new
        {
            Id = Guid.NewGuid().ToString(),
            ChordName = "Mapped Chord",
            Context = "Mapped Context",
            MappedAt = DateTime.UtcNow
        };
    }

    public static object ToDto(object chord)
    {
        return MapToDto(chord);
    }
}

/// <summary>
/// Chord error type enum
/// </summary>
public enum ChordErrorType
{
    InvalidChord,
    UnsupportedQuality,
    InvalidVoicing,
    ProcessingError,
    ValidationError,
    NotFound,
    DatabaseError
}

/// <summary>
/// Shape graph build options
/// </summary>
public class ShapeGraphBuildOptions
{
    public bool IncludeConnections { get; set; } = true;
    public int MaxDepth { get; set; } = 5;
    public int MaxFret { get; set; } = 12;
    public int MaxSpan { get; set; } = 4;
    public int MaxShapesPerSet { get; set; } = 10;
    public string Algorithm { get; set; } = "default";
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Chord context DTO
/// </summary>
public class ChordContextDto
{
    public string Id { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Scale { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public List<string> RelatedChords { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();

    // Additional properties needed by controllers
    public string Function { get; set; } = string.Empty;
    public string Degree { get; set; } = string.Empty;
    public Dictionary<string, object> Analysis { get; set; } = new();
}

/// <summary>
/// Fret position DTO
/// </summary>
public class FretPosition
{
    public int String { get; set; }
    public int Fret { get; set; }
    public string Note { get; set; } = string.Empty;
    public int Finger { get; set; }
    public bool IsMuted { get; set; }
}

/// <summary>
/// Voicing analysis DTO
/// </summary>
public class VoicingAnalysisDto
{
    public string Id { get; set; } = string.Empty;
    public double Difficulty { get; set; }
    public double Stretch { get; set; }
    public double Comfort { get; set; }
    public List<string> Intervals { get; set; } = new();
    public Dictionary<string, double> Metrics { get; set; } = new();

    // Additional properties needed by controllers
    public double Consonance { get; set; }
    public int BarreCount { get; set; }
    public List<int> MutedStrings { get; set; } = new();
    public List<int> OpenStrings { get; set; } = new();
    public int FretSpan { get; set; }
    public int LowestFret { get; set; }
    public int HighestFret { get; set; }
}
