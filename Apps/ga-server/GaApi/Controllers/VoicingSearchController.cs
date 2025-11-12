namespace GaApi.Controllers;

using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Data.MongoDB.Services.Embeddings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// API controller for voicing semantic search using GPU-accelerated vector search
/// </summary>
[ApiController]
[Route("api/voicings")]
[Produces("application/json")]
[EnableRateLimiting("semantic")]
public class VoicingSearchController(
    EnhancedVoicingSearchService voicingSearch,
    IEmbeddingService embeddingService,
    ILogger<VoicingSearchController> logger)
    : ControllerBase
{
    private static readonly SemaphoreSlim _initializationLock = new(1, 1);
    /// <summary>
    /// Semantic search: Find voicings using natural language queries
    /// </summary>
    /// <param name="q">Natural language query (e.g., "beginner friendly open position chords")</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="difficulty">Filter by difficulty (Beginner, Intermediate, Advanced)</param>
    /// <param name="position">Filter by position (Open Position, Middle Position, Upper Position)</param>
    /// <param name="voicingType">Filter by voicing type (Drop-2, Drop-3, Rootless, etc.)</param>
    /// <param name="modeName">Filter by mode name (Dorian, Phrygian, etc.)</param>
    /// <param name="tags">Filter by semantic tags (comma-separated)</param>
    /// <param name="minFret">Minimum fret number</param>
    /// <param name="maxFret">Maximum fret number</param>
    /// <param name="requireBarreChord">Filter for barre chords only</param>
    /// <returns>List of voicings ranked by semantic similarity</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<VoicingSearchResult>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(503)]
    public async Task<ActionResult<List<VoicingSearchResult>>> SemanticSearch(
        [FromQuery] string q,
        [FromQuery] int limit = 10,
        [FromQuery] string? difficulty = null,
        [FromQuery] string? position = null,
        [FromQuery] string? voicingType = null,
        [FromQuery] string? modeName = null,
        [FromQuery] string? tags = null,
        [FromQuery] int? minFret = null,
        [FromQuery] int? maxFret = null,
        [FromQuery] bool? requireBarreChord = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Query parameter 'q' is required");
            }

            if (!voicingSearch.IsInitialized)
            {
                return StatusCode(503, "Voicing search is not initialized. Please wait for indexing to complete.");
            }

            // Parse tags if provided
            string[]? tagArray = null;
            if (!string.IsNullOrWhiteSpace(tags))
            {
                tagArray = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }

            // Create filters if any are provided
            VoicingSearchFilters? filters = null;
            if (difficulty != null || position != null || voicingType != null || modeName != null ||
                tagArray != null || minFret != null || maxFret != null || requireBarreChord != null)
            {
                filters = new VoicingSearchFilters(
                    difficulty,
                    position,
                    voicingType,
                    modeName,
                    tagArray,
                    minFret,
                    maxFret,
                    requireBarreChord);
            }

            // TODO: Replace with actual embedding generator (Ollama, OpenAI, etc.)
            var results = await voicingSearch.SearchAsync(
                q,
                GenerateMockEmbedding,
                limit,
                filters);

            logger.LogInformation(
                "Voicing search for '{Query}' returned {Count} results using {Strategy}",
                q, results.Count, voicingSearch.StrategyName);

            return Ok(results);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Voicing search not initialized");
            return StatusCode(503, "Voicing search is not configured properly.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing voicing search for query: {Query}", q);
            return StatusCode(500, "Error performing voicing search");
        }
    }

    /// <summary>
    /// Find voicings similar to a specific voicing
    /// </summary>
    /// <param name="id">ID of the voicing to find similar voicings for</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <returns>List of similar voicings ranked by similarity</returns>
    [HttpGet("similar/{id}")]
    [ProducesResponseType(typeof(List<VoicingSearchResult>), 200)]
    [ProducesResponseType(404)]
    [ProducesResponseType(503)]
    public async Task<ActionResult<List<VoicingSearchResult>>> FindSimilar(
        string id,
        [FromQuery] int limit = 10)
    {
        try
        {
            if (!voicingSearch.IsInitialized)
            {
                return StatusCode(503, "Voicing search is not initialized. Please wait for indexing to complete.");
            }

            var results = await voicingSearch.FindSimilarAsync(id, limit);

            logger.LogInformation(
                "Found {Count} voicings similar to '{VoicingId}' using {Strategy}",
                results.Count, id, voicingSearch.StrategyName);

            return Ok(results);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Voicing not found: {VoicingId}", id);
            return NotFound($"Voicing with ID '{id}' not found");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Voicing search not initialized");
            return StatusCode(503, "Voicing search is not configured properly.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error finding similar voicings for: {VoicingId}", id);
            return StatusCode(500, "Error finding similar voicings");
        }
    }

    /// <summary>
    /// Get voicing search statistics
    /// </summary>
    /// <returns>Statistics about the voicing search service</returns>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(VoicingSearchStatsDto), 200)]
    public ActionResult<VoicingSearchStatsDto> GetStats()
    {
        try
        {
            var stats = voicingSearch.GetStats();
            var performance = voicingSearch.Performance;

            var dto = new VoicingSearchStatsDto
            {
                StrategyName = voicingSearch.StrategyName,
                IsInitialized = voicingSearch.IsInitialized,
                IsAvailable = voicingSearch.IsAvailable,
                TotalVoicings = stats.TotalVoicings,
                MemoryUsageMb = stats.MemoryUsageMb,
                AverageSearchTimeMs = stats.AverageSearchTime.TotalMilliseconds,
                TotalSearches = stats.TotalSearches,
                ExpectedSearchTimeMs = performance.ExpectedSearchTime.TotalMilliseconds,
                RequiresGpu = performance.RequiresGpu,
                RequiresNetwork = performance.RequiresNetwork
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting voicing search stats");
            return StatusCode(500, "Error retrieving statistics");
        }
    }

    /// <summary>
    /// Get all indexed voicings (for debugging/testing)
    /// </summary>
    /// <param name="limit">Maximum number of voicings to return</param>
    /// <returns>List of indexed voicing documents</returns>
    [HttpGet("indexed")]
    [ProducesResponseType(typeof(List<VoicingDocument>), 200)]
    public ActionResult<List<VoicingDocument>> GetIndexedVoicings([FromQuery] int limit = 100)
    {
        try
        {
            // This would need to be exposed by the indexing service
            // For now, return a message
            return Ok(new { message = $"Total indexed voicings: {voicingSearch.DocumentCount}", limit });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting indexed voicings");
            return StatusCode(500, "Error retrieving indexed voicings");
        }
    }

    /// <summary>
    /// Helper method to ensure voicing search is initialized (supports lazy loading)
    /// </summary>
    private async Task<bool> EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        // Already initialized - fast path
        if (voicingSearch.IsInitialized)
            return true;

        // Use semaphore to ensure only one initialization happens
        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (voicingSearch.IsInitialized)
                return true;

            logger.LogInformation("Lazy loading voicing search index...");

            // Initialize embeddings
            await voicingSearch.InitializeEmbeddingsAsync(
                async text =>
                {
                    var embedding = await embeddingService.GenerateEmbeddingAsync(text);
                    return embedding.Select(f => (double)f).ToArray();
                },
                cancellationToken);

            logger.LogInformation("Voicing search index lazy loaded successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to lazy load voicing search index");
            return false;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    // TODO: Replace with actual embedding generator
    private static async Task<double[]> GenerateMockEmbedding(string text)
    {
        await Task.Delay(1); // Simulate async operation

        var random = new Random(text.GetHashCode());
        // Use 768 dimensions to match the cached embeddings (mxbai-embed-large model)
        var embedding = new double[768];

        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] = random.NextDouble() * 2 - 1; // Range: -1 to 1
        }

        // Normalize
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }
}

/// <summary>
/// DTO for voicing search statistics
/// </summary>
public class VoicingSearchStatsDto
{
    public required string StrategyName { get; set; }
    public bool IsInitialized { get; set; }
    public bool IsAvailable { get; set; }
    public long TotalVoicings { get; set; }
    public long MemoryUsageMb { get; set; }
    public double AverageSearchTimeMs { get; set; }
    public long TotalSearches { get; set; }
    public double ExpectedSearchTimeMs { get; set; }
    public bool RequiresGpu { get; set; }
    public bool RequiresNetwork { get; set; }
}


