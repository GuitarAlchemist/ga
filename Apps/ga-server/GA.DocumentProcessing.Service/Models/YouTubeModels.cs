namespace GA.DocumentProcessing.Service.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// YouTube video transcript with timestamped segments
/// </summary>
public class YouTubeTranscript
{
    public string VideoId { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Channel { get; set; }
    public TimeSpan? Duration { get; set; }
    public List<TranscriptSegment> Segments { get; set; } = new();
    public string FullText { get; set; } = string.Empty;
    public DateTime ExtractedAt { get; set; }
}

/// <summary>
/// Individual transcript segment with timestamp
/// </summary>
public class TranscriptSegment
{
    public string Text { get; set; } = string.Empty;
    public TimeSpan Start { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// YouTube video metadata stored in MongoDB
/// </summary>
public class YouTubeVideoDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string VideoId { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Channel { get; set; }
    public string? Description { get; set; }
    public TimeSpan? Duration { get; set; }
    public DateTime? PublishedAt { get; set; }

    // Transcript data
    public string FullTranscript { get; set; } = string.Empty;
    public List<TranscriptSegment> TranscriptSegments { get; set; } = new();
    public DateTime TranscriptExtractedAt { get; set; }

    // Processing metadata
    public string? ProcessedDocumentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Tags and categorization
    public List<string> Tags { get; set; } = new();
    public string? Category { get; set; }

    // Extracted knowledge references
    public List<string> ExtractedChordProgressions { get; set; } = new();
    public List<string> ExtractedScales { get; set; } = new();
    public List<string> ExtractedTechniques { get; set; } = new();
}

/// <summary>
/// Request to process YouTube video
/// </summary>
public class ProcessYouTubeVideoRequest
{
    public string YouTubeUrl { get; set; } = string.Empty;
    public List<string>? Tags { get; set; }
    public string? Category { get; set; }
    public bool ExtractKnowledge { get; set; } = true;
}

/// <summary>
/// Response from YouTube video processing
/// </summary>
public class ProcessYouTubeVideoResponse
{
    public string VideoId { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Channel { get; set; }
    public int TranscriptLength { get; set; }
    public int SegmentCount { get; set; }
    public string? ProcessedDocumentId { get; set; }
    public ExtractedKnowledge? ExtractedKnowledge { get; set; }
}

/// <summary>
/// Request to start retroaction loop with YouTube video
/// </summary>
public class StartYouTubeRetroactionLoopRequest
{
    public string YouTubeUrl { get; set; } = string.Empty;
    public string Focus { get; set; } = "music theory, guitar techniques, chord progressions";
    public int MaxIterations { get; set; } = 5;
    public double ConvergenceThreshold { get; set; } = 0.85;
}

