namespace GaApi.Controllers;

using System.Runtime.CompilerServices;
using GA.Business.Core.Fretboard.Biomechanics;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
///     API controller for semantic search across guitar datasets
///     Provides natural language search for chords, voicings, and techniques
/// </summary>
[ApiController]
[Route("api/semantic-search")]
[EnableRateLimiting("fixed")]
public class SemanticSearchController(
    SemanticSearchService searchService,
    ILogger<SemanticSearchController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Search for chords and voicings using natural language
    /// </summary>
    /// <param name="query">Natural language search query (e.g., "easy jazz chords for small hands")</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="category">Optional category filter (e.g., "Chord Templates", "Chord Voicings")</param>
    /// <param name="minPlayabilityScore">Minimum playability score (0-1)</param>
    /// <param name="maxPlayabilityScore">Maximum playability score (0-1)</param>
    /// <param name="difficulty">Difficulty level filter</param>
    /// <param name="isPlayable">Filter by playability</param>
    /// <param name="hasBarreChord">Filter by barre chord requirement</param>
    /// <param name="usesPinky">Filter by pinky finger usage</param>
    /// <param name="isErgonomic">Filter by ergonomic wrist posture</param>
    /// <param name="minFret">Minimum fret position</param>
    /// <param name="maxFret">Maximum fret position</param>
    /// <param name="maxFretSpan">Maximum fret span</param>
    /// <param name="chordQuality">Chord quality filter (e.g., "Major", "Minor", "Dominant")</param>
    /// <param name="handSize">Hand size for personalized results</param>
    [HttpGet("search")]
    [ProducesResponseType(typeof(SearchResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<SearchResponse>> Search(
        [FromQuery] string query,
        [FromQuery] int limit = 10,
        [FromQuery] string? category = null,
        [FromQuery] double? minPlayabilityScore = null,
        [FromQuery] double? maxPlayabilityScore = null,
        [FromQuery] string? difficulty = null,
        [FromQuery] bool? isPlayable = null,
        [FromQuery] bool? hasBarreChord = null,
        [FromQuery] bool? usesPinky = null,
        [FromQuery] bool? isErgonomic = null,
        [FromQuery] int? minFret = null,
        [FromQuery] int? maxFret = null,
        [FromQuery] int? maxFretSpan = null,
        [FromQuery] string? chordQuality = null,
        [FromQuery] HandSize? handSize = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Query parameter is required" });
        }

        try
        {
            var filters = new SemanticSearchService.SearchFilters(
                Category: category,
                MinPlayabilityScore: minPlayabilityScore,
                MaxPlayabilityScore: maxPlayabilityScore,
                Difficulty: difficulty,
                IsPlayable: isPlayable,
                HasBarreChord: hasBarreChord,
                UsesPinky: usesPinky,
                IsErgonomic: isErgonomic,
                MinFret: minFret,
                MaxFret: maxFret,
                MaxFretSpan: maxFretSpan,
                ChordQuality: chordQuality,
                HandSize: handSize);

            var results = await searchService.SearchAsync(query, limit, filters);

            logger.LogInformation(
                "Semantic search: query='{Query}', results={Count}, filters={Filters}",
                query, results.Count, filters);

            return Ok(new SearchResponse
            {
                Query = query,
                TotalResults = results.Count,
                Results = results.Select(r => new SearchResultDto
                {
                    Id = r.Id,
                    Content = r.Content,
                    Category = r.Category,
                    Score = r.Score,
                    MatchReason = r.MatchReason,
                    Metadata = r.Metadata
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error performing semantic search for query: {Query}", query);
            return StatusCode(500, new { error = "An error occurred during search" });
        }
    }

    /// <summary>
    ///     Stream search results for progressive rendering (memory-efficient)
    /// </summary>
    /// <param name="query">Natural language search query</param>
    /// <param name="limit">Maximum number of results to return</param>
    /// <param name="category">Optional category filter</param>
    /// <param name="minPlayabilityScore">Minimum playability score (0-1)</param>
    /// <param name="maxPlayabilityScore">Maximum playability score (0-1)</param>
    /// <param name="difficulty">Difficulty level filter</param>
    /// <param name="isPlayable">Filter by playability</param>
    /// <param name="hasBarreChord">Filter by barre chord requirement</param>
    /// <param name="usesPinky">Filter by pinky finger usage</param>
    /// <param name="isErgonomic">Filter by ergonomic wrist posture</param>
    /// <param name="minFret">Minimum fret position</param>
    /// <param name="maxFret">Maximum fret position</param>
    /// <param name="maxFretSpan">Maximum fret span</param>
    /// <param name="chordQuality">Chord quality filter</param>
    /// <param name="handSize">Hand size for personalized results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("search/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<SearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(400)]
    public async IAsyncEnumerable<SearchResultDto> SearchStream(
        [FromQuery] string query,
        [FromQuery] int limit = 10,
        [FromQuery] string? category = null,
        [FromQuery] double? minPlayabilityScore = null,
        [FromQuery] double? maxPlayabilityScore = null,
        [FromQuery] string? difficulty = null,
        [FromQuery] bool? isPlayable = null,
        [FromQuery] bool? hasBarreChord = null,
        [FromQuery] bool? usesPinky = null,
        [FromQuery] bool? isErgonomic = null,
        [FromQuery] int? minFret = null,
        [FromQuery] int? maxFret = null,
        [FromQuery] int? maxFretSpan = null,
        [FromQuery] string? chordQuality = null,
        [FromQuery] HandSize? handSize = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            logger.LogWarning("Empty query provided to search stream");
            yield break;
        }

        logger.LogInformation("Streaming semantic search: query='{Query}', limit={Limit}", query, limit);

        var filters = new SemanticSearchService.SearchFilters(
            Category: category,
            MinPlayabilityScore: minPlayabilityScore,
            MaxPlayabilityScore: maxPlayabilityScore,
            Difficulty: difficulty,
            IsPlayable: isPlayable,
            HasBarreChord: hasBarreChord,
            UsesPinky: usesPinky,
            IsErgonomic: isErgonomic,
            MinFret: minFret,
            MaxFret: maxFret,
            MaxFretSpan: maxFretSpan,
            ChordQuality: chordQuality,
            HandSize: handSize);

        // Get results and stream them one at a time
        var results = await searchService.SearchAsync(query, limit, filters);
        var resultCount = 0;

        foreach (var result in results)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Search streaming cancelled after {ResultCount} results", resultCount);
                yield break;
            }

            resultCount++;

            yield return new SearchResultDto
            {
                Id = result.Id,
                Content = result.Content,
                Category = result.Category,
                Score = result.Score,
                MatchReason = result.MatchReason,
                Metadata = result.Metadata
            };

            // Log progress every 10 results
            if (resultCount % 10 == 0)
            {
                logger.LogDebug("Streamed {ResultCount} search results so far", resultCount);
            }

            // Backpressure control
            await Task.Delay(1, cancellationToken);
        }

        logger.LogInformation("Completed streaming {ResultCount} search results for query '{Query}'", resultCount,
            query);
    }

    /// <summary>
    ///     Get index statistics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(IndexStatsResponse), 200)]
    public ActionResult<IndexStatsResponse> GetStatistics()
    {
        var stats = searchService.GetStatistics();

        return Ok(new IndexStatsResponse
        {
            TotalDocuments = stats.TotalDocuments,
            DocumentsByCategory = stats.DocumentsByCategory,
            EmbeddingDimension = stats.EmbeddingDimension
        });
    }

    /// <summary>
    ///     Example queries for demonstration
    /// </summary>
    [HttpGet("examples")]
    [ProducesResponseType(typeof(ExamplesResponse), 200)]
    public ActionResult<ExamplesResponse> GetExamples()
    {
        var examples = new List<QueryExample>
        {
            new()
            {
                Query = "easy jazz chords for small hands",
                Description = "Find beginner-friendly jazz chords suitable for smaller hands",
                SuggestedFilters = new Dictionary<string, object>
                {
                    ["chordQuality"] = "Dominant",
                    ["maxFretSpan"] = 3,
                    ["isErgonomic"] = true
                }
            },
            new()
            {
                Query = "comfortable barre chords without pinky",
                Description = "Find barre chord voicings that don't require the pinky finger",
                SuggestedFilters = new Dictionary<string, object>
                {
                    ["hasBarreChord"] = true,
                    ["usesPinky"] = false,
                    ["isErgonomic"] = true
                }
            },
            new()
            {
                Query = "open position major chords",
                Description = "Find open position major chord voicings",
                SuggestedFilters = new Dictionary<string, object>
                {
                    ["chordQuality"] = "Major",
                    ["maxFret"] = 3,
                    ["difficulty"] = "Easy"
                }
            },
            new()
            {
                Query = "advanced extended chords with good playability",
                Description = "Find complex extended chords that are still playable",
                SuggestedFilters = new Dictionary<string, object>
                {
                    ["minPlayabilityScore"] = 0.7,
                    ["isPlayable"] = true
                }
            },
            new()
            {
                Query = "slide and legato techniques",
                Description = "Find chord voicings that support slide and legato playing",
                SuggestedFilters = new Dictionary<string, object>
                {
                    ["category"] = "Chord Voicings"
                }
            }
        };

        return Ok(new ExamplesResponse { Examples = examples });
    }
}

/// <summary>
///     Search response DTO
/// </summary>
public class SearchResponse
{
    public string Query { get; set; } = "";
    public int TotalResults { get; set; }
    public List<SearchResultDto> Results { get; set; } = [];
}

/// <summary>
///     Search result DTO
/// </summary>
public class SearchResultDto
{
    public string Id { get; set; } = "";
    public string Content { get; set; } = "";
    public string Category { get; set; } = "";
    public double Score { get; set; }
    public string MatchReason { get; set; } = "";
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
///     Index statistics response DTO
/// </summary>
public class IndexStatsResponse
{
    public int TotalDocuments { get; set; }
    public Dictionary<string, int> DocumentsByCategory { get; set; } = new();
    public int EmbeddingDimension { get; set; }
}

/// <summary>
///     Examples response DTO
/// </summary>
public class ExamplesResponse
{
    public List<QueryExample> Examples { get; set; } = [];
}

/// <summary>
///     Query example DTO
/// </summary>
public class QueryExample
{
    public string Query { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, object> SuggestedFilters { get; set; } = new();
}
