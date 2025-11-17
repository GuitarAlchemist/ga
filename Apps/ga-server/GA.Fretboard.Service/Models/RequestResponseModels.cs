namespace GA.Fretboard.Service.Models;

/// <summary>
/// Spice up progression request
/// </summary>
public class SpiceUpProgressionRequest
{
    public string ProgressionId { get; set; } = string.Empty;
    public List<string> Chords { get; set; } = new();
    public string Style { get; set; } = string.Empty;
    public int ComplexityLevel { get; set; } = 1;
    public Dictionary<string, object> Options { get; set; } = new();
    public List<string> Progression { get; set; } = new();
}

/// <summary>
/// Reharmonize progression request
/// </summary>
public class ReharmonizeProgressionRequest
{
    public string ProgressionId { get; set; } = string.Empty;
    public List<string> OriginalChords { get; set; } = new();
    public string TargetStyle { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, object> Constraints { get; set; } = new();
    public List<string> Progression { get; set; } = new();
}

/// <summary>
/// Create progression request
/// </summary>
public class CreateProgressionRequest
{
    public string Key { get; set; } = string.Empty;
    public string Style { get; set; } = string.Empty;
    public int Length { get; set; } = 4;
    public string Mood { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Hand pose response
/// </summary>
public class HandPoseResponse
{
    public string Id { get; set; } = string.Empty;
    public string ChordName { get; set; } = string.Empty;
    public Dictionary<string, object> HandPosition { get; set; } = new();
    public List<FingerPosition> FingerPositions { get; set; } = new();
    public double Difficulty { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Finger position
/// </summary>
public class FingerPosition
{
    public int Finger { get; set; }
    public int String { get; set; }
    public int Fret { get; set; }
    public string Pressure { get; set; } = string.Empty;
}

/// <summary>
/// Guitar mapping request
/// </summary>
public class GuitarMappingRequest
{
    public string ChordName { get; set; } = string.Empty;
    public string Tuning { get; set; } = "Standard";
    public int CapoPosition { get; set; } = 0;
    public List<string> PreferredShapes { get; set; } = new();
    public Dictionary<string, object> Constraints { get; set; } = new();
    public string HandPose { get; set; } = string.Empty;
    public string NeckConfig { get; set; } = string.Empty;
    public string HandToMap { get; set; } = string.Empty;
}

/// <summary>
/// Guitar mapping response
/// </summary>
public class GuitarMappingResponse
{
    public string Id { get; set; } = string.Empty;
    public string ChordName { get; set; } = string.Empty;
    public List<FretboardPosition> Positions { get; set; } = new();
    public Dictionary<string, object> MappingData { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Fretboard position
/// </summary>
public class FretboardPosition
{
    public int String { get; set; }
    public int Fret { get; set; }
    public string Note { get; set; } = string.Empty;
    public bool IsMuted { get; set; }
}

/// <summary>
/// Sound generation request
/// </summary>
public class SoundGenerationRequest
{
    public string ChordName { get; set; } = string.Empty;
    public string Instrument { get; set; } = "Guitar";
    public string Style { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public int String { get; set; }
    public int Fret { get; set; }
}

/// <summary>
/// Job response
/// </summary>
public class JobResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Job status response
/// </summary>
public class JobStatusResponse
{
    public string JobId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double Progress { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Result { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Search response
/// </summary>
public class SearchResponse
{
    public string Id { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public List<SearchResult> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Search result
/// </summary>
public class SearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Relevance { get; set; }
    public Dictionary<string, object> Data { get; set; } = new();
}
