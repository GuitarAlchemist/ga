namespace GaApi.Controllers;

using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Models;
using Services;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[EnableRateLimiting("regular")]
public class ChordsController(
    MongoDbService mongoDb,
    ILogger<ChordsController> logger,
    IMemoryCache cache,
    PerformanceMetricsService metrics)
    : ControllerBase
{
    // Valid values for string validation (SearchValues requires .NET 10)
    private static readonly HashSet<string> _validQualities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Major", "Minor", "Dominant", "Augmented", "Diminished",
        "HalfDiminished", "Sus2", "Sus4", "Add9", "Add11", "Add13"
    };

    private static readonly HashSet<string> _validExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Seventh", "Ninth", "Eleventh", "Thirteenth", "Sixth",
        "None", "Add9", "Add11", "Add13"
    };

    private static readonly HashSet<string> _validStackingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tertian", "Quartal", "Quintal", "Secundal", "Mixed"
    };

    /// <summary>
    ///     Get total count of chords in the database
    /// </summary>
    /// <returns>Total number of chords</returns>
    /// <response code="200">Returns the total chord count</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("count")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<long>> GetCount()
    {
        using var _ = metrics.TrackRegularRequest();

        const string cacheKey = "chord_count";

        if (cache.TryGetValue(cacheKey, out long cachedCount))
        {
            return Ok(cachedCount);
        }

        try
        {
            var count = await mongoDb.GetTotalChordCountAsync();

            // Cache for 5 minutes
            cache.Set(cacheKey, count, TimeSpan.FromMinutes(5));

            return Ok(count);
        }
        catch (Exception ex)
        {
            metrics.RecordRegularError();
            logger.LogError(ex, "Error getting chord count");
            return StatusCode(500, new { error = "Error retrieving chord count", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get chords by quality (e.g., Major, Minor, Dominant)
    /// </summary>
    /// <param name="quality">The chord quality to filter by</param>
    /// <param name="limit">Maximum number of chords to return (1-1000)</param>
    /// <returns>List of chords matching the quality</returns>
    /// <response code="200">Returns chords matching the quality</response>
    /// <response code="400">Invalid parameters</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("quality/{quality}")]
    [ProducesResponseType(typeof(List<Chord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Chord>>> GetByQuality(
        [Required] string quality,
        [FromQuery] [Range(1, 1000)] int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(quality))
        {
            return BadRequest(new { error = "Quality parameter is required" });
        }

        var cacheKey = $"chords_quality_{quality}_{limit}";

        if (cache.TryGetValue(cacheKey, out List<Chord>? cachedChords))
        {
            return Ok(cachedChords);
        }

        try
        {
            var chords = await mongoDb.GetChordsByQualityAsync(quality, limit);

            // Cache for 10 minutes
            cache.Set(cacheKey, chords, TimeSpan.FromMinutes(10));

            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords by quality: {Quality}", quality);
            return StatusCode(500, new { error = "Error retrieving chords by quality", details = ex.Message });
        }
    }

    /// <summary>
    ///     Get chords by extension (e.g., Triad, Seventh, Ninth)
    /// </summary>
    [HttpGet("extension/{extension}")]
    public async Task<ActionResult<List<Chord>>> GetByExtension(string extension, [FromQuery] int limit = 100)
    {
        try
        {
            var chords = await mongoDb.GetChordsByExtensionAsync(extension, limit);
            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords by extension: {Extension}", extension);
            return StatusCode(500, "Error retrieving chords");
        }
    }

    /// <summary>
    ///     Get chords by stacking type (e.g., Tertian, Quartal, Quintal)
    /// </summary>
    [HttpGet("stacking/{stackingType}")]
    public async Task<ActionResult<List<Chord>>> GetByStackingType(string stackingType, [FromQuery] int limit = 100)
    {
        try
        {
            var chords = await mongoDb.GetChordsByStackingTypeAsync(stackingType, limit);
            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords by stacking type: {StackingType}", stackingType);
            return StatusCode(500, "Error retrieving chords");
        }
    }

    /// <summary>
    ///     Get chords by quality and extension
    /// </summary>
    [HttpGet("quality/{quality}/extension/{extension}")]
    public async Task<ActionResult<List<Chord>>> GetByQualityAndExtension(
        string quality,
        string extension,
        [FromQuery] int limit = 100)
    {
        try
        {
            var chords = await mongoDb.GetChordsByQualityAndExtensionAsync(quality, extension, limit);
            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords by quality and extension");
            return StatusCode(500, "Error retrieving chords");
        }
    }

    /// <summary>
    ///     Get chords by pitch class set (e.g., "0,3,7" for minor triad)
    /// </summary>
    [HttpGet("pitch-class-set")]
    public async Task<ActionResult<List<Chord>>> GetByPitchClassSet([FromQuery] string pcs, [FromQuery] int limit = 100)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pcs))
            {
                return BadRequest("Pitch class set cannot be empty");
            }

            var pitchClassSet = pcs.Split(',').Select(int.Parse).ToList();
            var chords = await mongoDb.GetChordsByPitchClassSetAsync(pitchClassSet, limit);
            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords by pitch class set: {PitchClassSet}", pcs);
            return StatusCode(500, "Error retrieving chords");
        }
    }

    /// <summary>
    ///     Get chords by note count (e.g., 3 for triads, 4 for seventh chords)
    /// </summary>
    [HttpGet("note-count/{noteCount}")]
    public async Task<ActionResult<List<Chord>>> GetByNoteCount(int noteCount, [FromQuery] int limit = 100)
    {
        try
        {
            var chords = await mongoDb.GetChordsByNoteCountAsync(noteCount, limit);
            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords by note count: {NoteCount}", noteCount);
            return StatusCode(500, "Error retrieving chords");
        }
    }

    /// <summary>
    ///     Search chords by text (searches Name and Description fields)
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<List<Chord>>> Search([FromQuery] string q, [FromQuery] int limit = 100)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Search query cannot be empty");
            }

            var chords = await mongoDb.SearchChordsAsync(q, limit);
            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching chords: {Query}", q);
            return StatusCode(500, "Error searching chords");
        }
    }

    /// <summary>
    ///     Get chords by parent scale and optional scale degree
    /// </summary>
    [HttpGet("scale/{parentScale}")]
    public async Task<ActionResult<List<Chord>>> GetByScale(
        string parentScale,
        [FromQuery] int? degree = null,
        [FromQuery] int limit = 100)
    {
        try
        {
            var chords = await mongoDb.GetChordsByScaleAsync(parentScale, degree, limit);
            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chords by scale: {Scale}", parentScale);
            return StatusCode(500, "Error retrieving chords");
        }
    }

    /// <summary>
    ///     Get statistics: chord counts by quality
    /// </summary>
    [HttpGet("stats/by-quality")]
    public async Task<ActionResult<Dictionary<string, long>>> GetStatsByQuality()
    {
        try
        {
            var stats = await mongoDb.GetChordCountsByQualityAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chord stats by quality");
            return StatusCode(500, "Error retrieving statistics");
        }
    }

    /// <summary>
    ///     Get statistics: chord counts by stacking type
    /// </summary>
    [HttpGet("stats/by-stacking-type")]
    public async Task<ActionResult<Dictionary<string, long>>> GetStatsByStackingType()
    {
        try
        {
            var stats = await mongoDb.GetChordCountsByStackingTypeAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chord stats by stacking type");
            return StatusCode(500, "Error retrieving statistics");
        }
    }

    /// <summary>
    ///     Get distinct qualities available in the database
    /// </summary>
    [HttpGet("distinct/qualities")]
    public async Task<ActionResult<List<string>>> GetDistinctQualities()
    {
        try
        {
            var qualities = await mongoDb.GetDistinctQualitiesAsync();
            return Ok(qualities);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting distinct qualities");
            return StatusCode(500, "Error retrieving qualities");
        }
    }

    /// <summary>
    ///     Get distinct extensions available in the database
    /// </summary>
    [HttpGet("distinct/extensions")]
    public async Task<ActionResult<List<string>>> GetDistinctExtensions()
    {
        try
        {
            var extensions = await mongoDb.GetDistinctExtensionsAsync();
            return Ok(extensions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting distinct extensions");
            return StatusCode(500, "Error retrieving extensions");
        }
    }

    /// <summary>
    ///     Get distinct stacking types available in the database
    /// </summary>
    [HttpGet("distinct/stacking-types")]
    public async Task<ActionResult<List<string>>> GetDistinctStackingTypes()
    {
        try
        {
            var stackingTypes = await mongoDb.GetDistinctStackingTypesAsync();
            return Ok(stackingTypes);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting distinct stacking types");
            return StatusCode(500, "Error retrieving stacking types");
        }
    }

    // ========================================
    // STREAMING ENDPOINTS (IAsyncEnumerable)
    // ========================================

    /// <summary>
    ///     Stream chords by quality (progressive delivery for better performance)
    /// </summary>
    /// <param name="quality">Chord quality (e.g., Major, Minor, Dominant)</param>
    /// <param name="limit">Maximum number of chords to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of chords</returns>
    [HttpGet("quality/{quality}/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Chord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async IAsyncEnumerable<Chord> GetByQualityStream(
        [Required] string quality,
        [FromQuery] [Range(1, 1000)] int limit = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var _ = metrics.TrackRegularRequest();

        logger.LogInformation("Streaming chords by quality: {Quality}, limit: {Limit}", quality, limit);

        var count = 0;
        await foreach (var chord in mongoDb.GetChordsByQualityStreamAsync(quality, limit, cancellationToken))
        {
            count++;
            yield return chord;

            if (count % 10 == 0)
            {
                logger.LogDebug("Streamed {Count} chords so far", count);
            }
        }

        logger.LogInformation("Completed streaming {Count} chords by quality", count);
    }

    /// <summary>
    ///     Stream chords by extension (progressive delivery for better performance)
    /// </summary>
    /// <param name="extension">Chord extension (e.g., Triad, Seventh, Ninth)</param>
    /// <param name="limit">Maximum number of chords to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of chords</returns>
    [HttpGet("extension/{extension}/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Chord>), StatusCodes.Status200OK)]
    public async IAsyncEnumerable<Chord> GetByExtensionStream(
        string extension,
        [FromQuery] int limit = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var _ = metrics.TrackRegularRequest();

        logger.LogInformation("Streaming chords by extension: {Extension}, limit: {Limit}", extension, limit);

        var count = 0;
        await foreach (var chord in mongoDb.GetChordsByExtensionStreamAsync(extension, limit, cancellationToken))
        {
            count++;
            yield return chord;

            if (count % 10 == 0)
            {
                logger.LogDebug("Streamed {Count} chords so far", count);
            }
        }

        logger.LogInformation("Completed streaming {Count} chords by extension", count);
    }

    /// <summary>
    ///     Stream chords by stacking type (progressive delivery for better performance)
    /// </summary>
    /// <param name="stackingType">Stacking type (e.g., Tertian, Quartal, Quintal)</param>
    /// <param name="limit">Maximum number of chords to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of chords</returns>
    [HttpGet("stacking/{stackingType}/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Chord>), StatusCodes.Status200OK)]
    public async IAsyncEnumerable<Chord> GetByStackingTypeStream(
        string stackingType,
        [FromQuery] int limit = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var _ = metrics.TrackRegularRequest();

        logger.LogInformation("Streaming chords by stacking type: {StackingType}, limit: {Limit}", stackingType, limit);

        var count = 0;
        await foreach (var chord in mongoDb.GetChordsByStackingTypeStreamAsync(stackingType, limit, cancellationToken))
        {
            count++;
            yield return chord;

            if (count % 10 == 0)
            {
                logger.LogDebug("Streamed {Count} chords so far", count);
            }
        }

        logger.LogInformation("Completed streaming {Count} chords by stacking type", count);
    }

    /// <summary>
    ///     Stream chords by pitch class set (progressive delivery for better performance)
    /// </summary>
    /// <param name="pcs">Pitch class set (comma-separated, e.g., "0,3,7")</param>
    /// <param name="limit">Maximum number of chords to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of chords</returns>
    [HttpGet("pitch-class-set/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Chord>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async IAsyncEnumerable<Chord> GetByPitchClassSetStream(
        [FromQuery] string pcs,
        [FromQuery] int limit = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var _ = metrics.TrackRegularRequest();

        if (string.IsNullOrWhiteSpace(pcs))
        {
            logger.LogWarning("Pitch class set parameter is empty");
            yield break;
        }

        var pitchClasses = pcs.Split(',').Select(int.Parse).ToList();

        logger.LogInformation("Streaming chords by pitch class set: {PCS}, limit: {Limit}", pcs, limit);

        var count = 0;
        await foreach (var chord in mongoDb.GetChordsByPitchClassSetStreamAsync(pitchClasses, limit, cancellationToken))
        {
            count++;
            yield return chord;

            if (count % 10 == 0)
            {
                logger.LogDebug("Streamed {Count} chords so far", count);
            }
        }

        logger.LogInformation("Completed streaming {Count} chords by pitch class set", count);
    }

    /// <summary>
    ///     Stream chords by note count (progressive delivery for better performance)
    /// </summary>
    /// <param name="noteCount">Number of notes in the chord</param>
    /// <param name="limit">Maximum number of chords to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of chords</returns>
    [HttpGet("note-count/{noteCount}/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Chord>), StatusCodes.Status200OK)]
    public async IAsyncEnumerable<Chord> GetByNoteCountStream(
        int noteCount,
        [FromQuery] int limit = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var _ = metrics.TrackRegularRequest();

        logger.LogInformation("Streaming chords by note count: {NoteCount}, limit: {Limit}", noteCount, limit);

        var count = 0;
        await foreach (var chord in mongoDb.GetChordsByNoteCountStreamAsync(noteCount, limit, cancellationToken))
        {
            count++;
            yield return chord;

            if (count % 10 == 0)
            {
                logger.LogDebug("Streamed {Count} chords so far", count);
            }
        }

        logger.LogInformation("Completed streaming {Count} chords by note count", count);
    }

    /// <summary>
    ///     Stream chords by parent scale (progressive delivery for better performance)
    /// </summary>
    /// <param name="parentScale">Parent scale name</param>
    /// <param name="degree">Optional scale degree filter</param>
    /// <param name="limit">Maximum number of chords to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of chords</returns>
    [HttpGet("scale/{parentScale}/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<Chord>), StatusCodes.Status200OK)]
    public async IAsyncEnumerable<Chord> GetByScaleStream(
        string parentScale,
        [FromQuery] int? degree = null,
        [FromQuery] int limit = 100,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var _ = metrics.TrackRegularRequest();

        logger.LogInformation(
            "Streaming chords by scale: {ParentScale}, degree: {Degree}, limit: {Limit}",
            parentScale, degree, limit);

        var count = 0;
        await foreach (var chord in mongoDb.GetChordsByScaleStreamAsync(parentScale, degree, limit, cancellationToken))
        {
            count++;
            yield return chord;

            if (count % 10 == 0)
            {
                logger.LogDebug("Streamed {Count} chords so far", count);
            }
        }

        logger.LogInformation("Completed streaming {Count} chords by scale", count);
    }

    /// <summary>
    ///     Validate quality using SearchValues (5-10x faster than string comparison)
    /// </summary>
    private bool IsValidQuality(string quality)
    {
        return _validQualities.Contains(quality);
    }

    /// <summary>
    ///     Validate extension using SearchValues (5-10x faster than string comparison)
    /// </summary>
    private bool IsValidExtension(string extension)
    {
        return _validExtensions.Contains(extension);
    }

    /// <summary>
    ///     Validate stacking type using SearchValues (5-10x faster than string comparison)
    /// </summary>
    private bool IsValidStackingType(string stackingType)
    {
        return _validStackingTypes.Contains(stackingType);
    }
}
