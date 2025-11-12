namespace GA.DocumentProcessing.Service.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Represents a processed music theory document stored in MongoDB
/// </summary>
public class ProcessedDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Original filename or URL
    /// </summary>
    public required string SourceName { get; set; }

    /// <summary>
    /// Document type (PDF, Markdown, URL)
    /// </summary>
    public required string DocumentType { get; set; }

    /// <summary>
    /// Raw extracted text
    /// </summary>
    public required string RawText { get; set; }

    /// <summary>
    /// Ollama-generated summary (Stage 1)
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Extracted music theory concepts
    /// </summary>
    public ExtractedKnowledge? Knowledge { get; set; }

    /// <summary>
    /// Vector embedding for semantic search
    /// </summary>
    public float[]? Embedding { get; set; }

    /// <summary>
    /// Multiple embeddings for chunked text
    /// </summary>
    public List<float[]> Embeddings { get; set; } = new();

    /// <summary>
    /// Processing status
    /// </summary>
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;

    /// <summary>
    /// Error message if processing failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Processing metadata
    /// </summary>
    public ProcessingMetadata Metadata { get; set; } = new();

    /// <summary>
    /// When the document was uploaded
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the document was last processed
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Extracted music theory knowledge
/// </summary>
public class ExtractedKnowledge
{
    /// <summary>
    /// Chord progressions mentioned
    /// </summary>
    public List<string> ChordProgressions { get; set; } = new();

    /// <summary>
    /// Scales and modes discussed
    /// </summary>
    public List<string> Scales { get; set; } = new();

    /// <summary>
    /// Guitar techniques described
    /// </summary>
    public List<string> Techniques { get; set; } = new();

    /// <summary>
    /// Key concepts and definitions
    /// </summary>
    public Dictionary<string, string> Concepts { get; set; } = new();

    /// <summary>
    /// Musical examples (notation, tabs, etc.)
    /// </summary>
    public List<string> Examples { get; set; } = new();

    /// <summary>
    /// Artists or styles referenced
    /// </summary>
    public List<string> Styles { get; set; } = new();
}

/// <summary>
/// Processing metadata
/// </summary>
public class ProcessingMetadata
{
    public int CharacterCount { get; set; }
    public int WordCount { get; set; }
    public int PageCount { get; set; }
    public TimeSpan ProcessingDuration { get; set; }
    public string? OllamaModel { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Processing status
/// </summary>
public enum ProcessingStatus
{
    Pending,
    Extracting,
    Summarizing,
    ExtractingKnowledge,
    GeneratingEmbeddings,
    Embedding,
    Completed,
    Failed
}

