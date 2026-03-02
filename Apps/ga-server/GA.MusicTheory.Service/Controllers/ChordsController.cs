namespace GA.MusicTheory.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using Models;
using Services;

/// <summary>
/// Simple, intuitive API for querying chords
/// Examples:
///   GET /api/chords?root=C&quality=major7
///   GET /api/chords?quality=minor&notes=3
///   GET /api/chords?search=dominant
/// </summary>
[ApiController]
[Route("api/music-theory/chords")]
[Produces("application/json")]
public class ChordsController(
    MongoDbService mongoDb,
    ILogger<ChordsController> logger)
    : ControllerBase
{

    /// <summary>
    /// Query chords with flexible filters
    /// </summary>
    /// <param name="root">Root note (e.g., C, D, F#)</param>
    /// <param name="quality">Chord quality (e.g., major, minor, dominant, diminished)</param>
    /// <param name="extension">Extension (e.g., 7, 9, 11, 13)</param>
    /// <param name="stackingType">Stacking type (e.g., tertiary, quartal, cluster)</param>
    /// <param name="notes">Number of notes (e.g., 3 for triads, 4 for seventh chords)</param>
    /// <param name="search">Text search across chord names</param>
    /// <param name="limit">Maximum results (default: 20, max: 100)</param>
    /// <returns>List of matching chords</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<Chord>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Chord>>> GetChords(
        [FromQuery] string? root = null,
        [FromQuery] string? quality = null,
        [FromQuery] string? extension = null,
        [FromQuery] string? stackingType = null,
        [FromQuery] int? notes = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 20)
    {
        try
        {
            // Validate limit
            if (limit < 1 || limit > 100)
            {
                return BadRequest(new ErrorResponse { Error = "ValidationError", Message = "Limit must be between 1 and 100" });
            }

            // Build query based on provided filters
            List<Chord> chords;

            if (!string.IsNullOrWhiteSpace(search))
            {
                chords = await mongoDb.SearchChordsAsync(search, limit);
            }
            else
            {
                // Unifies root, quality, extension, stackingType, and noteCount filtering
                chords = await mongoDb.QueryChordsAsync(root, quality, extension, stackingType, notes, limit);
            }

            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying chords");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Error retrieving chords", Details = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific chord by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Chord), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<Chord>> GetById(string id)
    {
        try
        {
            var chord = await mongoDb.GetChordByIdAsync(id);
            if (chord == null) return NotFound(new ErrorResponse { Error = "NotFound", Message = $"Chord {id} not found" });
            return Ok(chord);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chord {Id}", id);
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Error retrieving chord", Details = ex.Message });
        }
    }

    /// <summary>
    /// Get chords similar to a specific chord
    /// </summary>
    [HttpGet("{id}/similar")]
    [ProducesResponseType(typeof(List<Chord>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<Chord>>> GetSimilar(string id, [FromQuery] int limit = 10)
    {
        try
        {
            var chords = await mongoDb.GetSimilarChordsAsync(id, limit);
            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting similar chords for {Id}", id);
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Error retrieving similar chords", Details = ex.Message });
        }
    }

    /// <summary>
    /// Get the total count of chords in the database
    /// </summary>
    [HttpGet("count")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<long>> GetTotalCount()
    {
        try
        {
            var count = await mongoDb.GetTotalChordCountAsync();
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chord count");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Error retrieving chord count", Details = ex.Message });
        }
    }

    /// <summary>
    /// Get available chord qualities
    /// </summary>
    [HttpGet("qualities")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<List<string>>> GetQualities()
    {
        try
        {
            var qualities = await mongoDb.GetDistinctQualitiesAsync();
            return Ok(qualities);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting qualities");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Error retrieving qualities", Details = ex.Message });
        }
    }

    /// <summary>
    /// Get statistics about chords in the database
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult> GetStats()
    {
        try
        {
            var qualityStats = await mongoDb.GetChordCountsByQualityAsync();
            var stackingStats = await mongoDb.GetChordCountsByStackingTypeAsync();
            var totalCount = await mongoDb.GetTotalChordCountAsync();

            return Ok(new
            {
                total = totalCount,
                byQuality = qualityStats,
                byStackingType = stackingStats
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chord statistics");
            return StatusCode(500, new ErrorResponse { Error = "InternalServerError", Message = "Error retrieving statistics", Details = ex.Message });
        }
    }
}
