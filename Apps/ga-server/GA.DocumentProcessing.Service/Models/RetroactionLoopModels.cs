namespace GA.DocumentProcessing.Service.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
/// Request to start a retroaction loop
/// </summary>
public class RetroactionLoopRequest
{
    public List<string> InitialDocuments { get; set; } = new();
    public string Focus { get; set; } = string.Empty;
    public int MaxIterations { get; set; } = 5;
    public double ConvergenceThreshold { get; set; } = 0.8;
}

/// <summary>
/// Result of a complete retroaction loop
/// </summary>
public class RetroactionLoopResult
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public string LoopId { get; set; } = string.Empty;

    public List<RetroactionIteration> Iterations { get; set; } = new();
    public double ConvergenceScore { get; set; }
    public bool Converged { get; set; }
    public int TotalIterations { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Single iteration in the retroaction loop
/// </summary>
public class RetroactionIteration
{
    public int IterationNumber { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }

    // NotebookLM outputs
    public int NotebookLMPodcastSize { get; set; }
    public string NotebookLMInsights { get; set; } = string.Empty;

    // Ollama outputs
    public string OllamaSummary { get; set; } = string.Empty;
    public ExtractedKnowledge? ExtractedKnowledge { get; set; }

    // Gap analysis
    public List<string> KnowledgeGaps { get; set; } = new();

    // Error tracking
    public string? Error { get; set; }
}

/// <summary>
/// DTO for starting a retroaction loop
/// </summary>
public class StartRetroactionLoopDto
{
    public List<string> Documents { get; set; } = new();
    public string Focus { get; set; } = string.Empty;
    public int MaxIterations { get; set; } = 5;
    public double ConvergenceThreshold { get; set; } = 0.8;
}

/// <summary>
/// DTO for retroaction loop status
/// </summary>
public class RetroactionLoopStatusDto
{
    public string LoopId { get; set; } = string.Empty;
    public int CurrentIteration { get; set; }
    public int TotalIterations { get; set; }
    public double ConvergenceScore { get; set; }
    public bool Converged { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<IterationSummaryDto> Iterations { get; set; } = new();
}

/// <summary>
/// Summary of a single iteration
/// </summary>
public class IterationSummaryDto
{
    public int IterationNumber { get; set; }
    public TimeSpan Duration { get; set; }
    public int PodcastSize { get; set; }
    public int KnowledgeGapsCount { get; set; }
    public List<string> TopGaps { get; set; } = new();
}

