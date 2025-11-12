namespace GaApi.GraphQL.Types;

using HotChocolate;
using Models.AutonomousCuration;
using Services.DocumentProcessing;

/// <summary>
/// GraphQL type for processed documents
/// </summary>
public class ProcessedDocumentType
{
    public string Id { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty; // youtube, pdf, markdown, web
    public string SourceId { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty; // Pending, Processing, Completed, Failed
    public int ChunkCount { get; set; }
    public ExtractedKnowledgeType? ExtractedKnowledge { get; set; }
    public DocumentMetadataType? Metadata { get; set; }
}

/// <summary>
/// GraphQL type for extracted knowledge
/// </summary>
public class ExtractedKnowledgeType
{
    public List<string> ChordProgressions { get; set; } = new();
    public List<string> Scales { get; set; } = new();
    public List<string> Techniques { get; set; } = new();
    public List<string> Concepts { get; set; } = new();
    public List<string> KeyInsights { get; set; } = new();
}

/// <summary>
/// GraphQL type for document metadata
/// </summary>
public class DocumentMetadataType
{
    public string? VideoId { get; set; }
    public string? ChannelName { get; set; }
    public long? ViewCount { get; set; }
    public TimeSpan? Duration { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? ThumbnailUrl { get; set; }
    public double? QualityScore { get; set; }
    public double? RelevanceScore { get; set; }
    public double? EducationalValueScore { get; set; }
    public double? EngagementScore { get; set; }
}

/// <summary>
/// GraphQL type for knowledge gaps
/// </summary>
public class KnowledgeGapType
{
    public string Category { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string SuggestedSearchQuery { get; set; } = string.Empty;
    public string PriorityReason { get; set; } = string.Empty;
    public List<string> DependentTopics { get; set; } = new();
    public int EstimatedLearningTimeMinutes { get; set; }
}

/// <summary>
/// GraphQL type for knowledge gap analysis
/// </summary>
public class KnowledgeGapAnalysisType
{
    public DateTime AnalysisDate { get; set; }
    public List<KnowledgeGapType> Gaps { get; set; } = new();
    public int TotalGaps { get; set; }
}

/// <summary>
/// GraphQL type for curation decisions
/// </summary>
public class CurationDecisionType
{
    public DateTime DecisionTime { get; set; }
    public string Action { get; set; } = string.Empty;
    public string VideoId { get; set; } = string.Empty;
    public string VideoTitle { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public KnowledgeGapType? RelatedGap { get; set; }
    public double QualityScore { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public List<string> PositiveFactors { get; set; } = new();
    public List<string> NegativeFactors { get; set; } = new();
}

/// <summary>
/// GraphQL input type for document upload
/// </summary>
public class DocumentUploadInput
{
    public string SourceType { get; set; } = string.Empty; // youtube, pdf, markdown, web
    public string SourceUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Content { get; set; } // For markdown/text content
}

/// <summary>
/// GraphQL input type for document search
/// </summary>
public class DocumentSearchInput
{
    public string? Query { get; set; }
    public List<string>? SourceTypes { get; set; }
    public List<string>? Categories { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public double? MinQualityScore { get; set; }
    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 20;
}

/// <summary>
/// GraphQL type for document search results
/// </summary>
public class DocumentSearchResultType
{
    public List<ProcessedDocumentType> Documents { get; set; } = new();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public bool HasMore { get; set; }
}

/// <summary>
/// GraphQL type for document processing status
/// </summary>
public class DocumentProcessingStatusType
{
    public string DocumentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; } // 0-100
    public string? CurrentStep { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// GraphQL payload for document upload mutation
/// </summary>
public class DocumentUploadPayload
{
    public ProcessedDocumentType? Document { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DocumentId { get; set; }
}

/// <summary>
/// GraphQL payload for document deletion mutation
/// </summary>
public class DocumentDeletePayload
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DeletedDocumentId { get; set; }
}

/// <summary>
/// GraphQL payload for autonomous curation mutation
/// </summary>
public class AutonomousCurationPayload
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int ProcessedVideos { get; set; }
    public int AcceptedVideos { get; set; }
    public List<CurationDecisionType> Decisions { get; set; } = new();
}

