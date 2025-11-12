namespace GA.DocumentProcessing.Service.Models;

/// <summary>
/// Request to upload a document from file
/// </summary>
public class UploadDocumentRequest
{
    public required IFormFile File { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Request to process a document from URL
/// </summary>
public class ProcessUrlRequest
{
    public required string Url { get; set; }
    public string? DocumentType { get; set; } // pdf, markdown, html
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Response after document upload
/// </summary>
public class DocumentUploadResponse
{
    public required string DocumentId { get; set; }
    public required string SourceName { get; set; }
    public required string Status { get; set; }
    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// Document processing result
/// </summary>
public class DocumentProcessingResult
{
    public required string DocumentId { get; set; }
    public required string SourceName { get; set; }
    public required string Status { get; set; }
    public string? Summary { get; set; }
    public ExtractedKnowledge? Knowledge { get; set; }
    public ProcessingMetadata? Metadata { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

/// <summary>
/// Search request for documents
/// </summary>
public class DocumentSearchRequest
{
    public required string Query { get; set; }
    public int MaxResults { get; set; } = 10;
    public List<string>? Tags { get; set; }
    public string? DocumentType { get; set; }
}

/// <summary>
/// Search result
/// </summary>
public class DocumentSearchResult
{
    public required string DocumentId { get; set; }
    public required string SourceName { get; set; }
    public required string Summary { get; set; }
    public double SimilarityScore { get; set; }
    public List<string> Tags { get; set; } = new();
    public ExtractedKnowledge? Knowledge { get; set; }
}

/// <summary>
/// Document statistics
/// </summary>
public class DocumentStatistics
{
    public int TotalDocuments { get; set; }
    public int ProcessedDocuments { get; set; }
    public int PendingDocuments { get; set; }
    public int FailedDocuments { get; set; }
    public Dictionary<string, int> DocumentsByType { get; set; } = new();
    public Dictionary<string, int> DocumentsByTag { get; set; } = new();
}

