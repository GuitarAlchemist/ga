namespace GA.DocumentProcessing.Service.Controllers;

using GA.DocumentProcessing.Service.Models;
using GA.DocumentProcessing.Service.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

/// <summary>
/// API controller for NotebookLM-style document processing
/// </summary>
[ApiController]
[Route("api/documents")]
[EnableRateLimiting("fixed")]
public class DocumentProcessingController : ControllerBase
{
    private readonly DocumentIngestionService _ingestionService;
    private readonly ILogger<DocumentProcessingController> _logger;

    public DocumentProcessingController(
        DocumentIngestionService ingestionService,
        ILogger<DocumentProcessingController> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    /// <summary>
    /// Upload and process a document (PDF, Markdown, or text file)
    /// </summary>
    [HttpPost("upload")]
    [SwaggerOperation(Summary = "Upload document", Description = "Upload a PDF, Markdown, or text file for processing")]
    [ProducesResponseType(typeof(ApiResponse<DocumentUploadResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> UploadDocument([FromForm] IFormFile file, [FromForm] string? tags = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(ApiResponse<object>.Fail("No file uploaded"));
            }

            var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
            if (!new[] { "pdf", "md", "markdown", "txt" }.Contains(extension))
            {
                return BadRequest(ApiResponse<object>.Fail($"Unsupported file type: {extension}"));
            }

            var tagList = string.IsNullOrWhiteSpace(tags)
                ? new List<string>()
                : tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();

            using var stream = file.OpenReadStream();
            var document = await _ingestionService.ProcessDocumentAsync(
                file.FileName,
                extension,
                stream,
                tagList);

            var response = new DocumentUploadResponse
            {
                DocumentId = document.Id!,
                SourceName = document.SourceName,
                Status = document.Status.ToString(),
                UploadedAt = document.CreatedAt
            };

            return Ok(ApiResponse<DocumentUploadResponse>.Ok(response, "Document uploaded and processing started"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading document");
            return StatusCode(500, ApiResponse<object>.Fail("Error uploading document", ex.Message));
        }
    }

    /// <summary>
    /// Process a document from URL
    /// </summary>
    [HttpPost("process-url")]
    [SwaggerOperation(Summary = "Process URL", Description = "Process a document from a URL")]
    [ProducesResponseType(typeof(ApiResponse<DocumentUploadResponse>), 200)]
    public async Task<IActionResult> ProcessUrl([FromBody] ProcessUrlRequest request)
    {
        try
        {
            // Download content from URL
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(request.Url);
            response.EnsureSuccessStatusCode();

            var contentType = request.DocumentType ?? "txt";
            using var stream = await response.Content.ReadAsStreamAsync();

            var document = await _ingestionService.ProcessDocumentAsync(
                request.Url,
                contentType,
                stream,
                request.Tags);

            var uploadResponse = new DocumentUploadResponse
            {
                DocumentId = document.Id!,
                SourceName = document.SourceName,
                Status = document.Status.ToString(),
                UploadedAt = document.CreatedAt
            };

            return Ok(ApiResponse<DocumentUploadResponse>.Ok(uploadResponse, "URL processing started"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing URL");
            return StatusCode(500, ApiResponse<object>.Fail("Error processing URL", ex.Message));
        }
    }

    /// <summary>
    /// Get document processing status and results
    /// </summary>
    [HttpGet("{documentId}")]
    [SwaggerOperation(Summary = "Get document", Description = "Get document processing status and results")]
    [ProducesResponseType(typeof(ApiResponse<DocumentProcessingResult>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetDocument(string documentId)
    {
        try
        {
            var document = await _ingestionService.GetDocumentAsync(documentId);
            if (document == null)
            {
                return NotFound(ApiResponse<object>.Fail($"Document {documentId} not found"));
            }

            var result = new DocumentProcessingResult
            {
                DocumentId = document.Id!,
                SourceName = document.SourceName,
                Status = document.Status.ToString(),
                Summary = document.Summary,
                Knowledge = document.Knowledge,
                Metadata = document.Metadata,
                ProcessedAt = document.ProcessedAt
            };

            return Ok(ApiResponse<DocumentProcessingResult>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting document {DocumentId}", documentId);
            return StatusCode(500, ApiResponse<object>.Fail("Error retrieving document", ex.Message));
        }
    }

    /// <summary>
    /// Search documents by semantic similarity
    /// </summary>
    [HttpPost("search")]
    [SwaggerOperation(Summary = "Search documents", Description = "Search documents using semantic similarity")]
    [ProducesResponseType(typeof(ApiResponse<List<DocumentSearchResult>>), 200)]
    public async Task<IActionResult> SearchDocuments([FromBody] DocumentSearchRequest request)
    {
        try
        {
            var documents = await _ingestionService.SearchDocumentsAsync(request.Query, request.MaxResults);

            var results = documents.Select(d => new DocumentSearchResult
            {
                DocumentId = d.Id!,
                SourceName = d.SourceName,
                Summary = d.Summary ?? string.Empty,
                SimilarityScore = 0.95, // Placeholder - would be calculated with actual vector search
                Tags = d.Tags,
                Knowledge = d.Knowledge
            }).ToList();

            return Ok(ApiResponse<List<DocumentSearchResult>>.Ok(results,
                metadata: new Dictionary<string, object> { ["totalResults"] = results.Count }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching documents");
            return StatusCode(500, ApiResponse<object>.Fail("Error searching documents", ex.Message));
        }
    }

    /// <summary>
    /// Get document processing statistics
    /// </summary>
    [HttpGet("statistics")]
    [SwaggerOperation(Summary = "Get statistics", Description = "Get document processing statistics")]
    [ProducesResponseType(typeof(ApiResponse<DocumentStatistics>), 200)]
    public async Task<IActionResult> GetStatistics()
    {
        try
        {
            var stats = await _ingestionService.GetStatisticsAsync();
            return Ok(ApiResponse<DocumentStatistics>.Ok(stats));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting statistics");
            return StatusCode(500, ApiResponse<object>.Fail("Error retrieving statistics", ex.Message));
        }
    }
}

