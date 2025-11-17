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
[Route("api/chords")]
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
    /// <param name="notes">Number of notes (e.g., 3 for triads, 4 for seventh chords)</param>
    /// <param name="search">Text search across chord names</param>
    /// <param name="limit">Maximum results (default: 20, max: 100)</param>
    /// <returns>List of matching chords</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<Chord>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Chord>>> GetChords(
        [FromQuery] string? root = null,
        [FromQuery] string? quality = null,
        [FromQuery] string? extension = null,
        [FromQuery] int? notes = null,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 20)
    {
        try
        {
            // Validate limit
            if (limit < 1 || limit > 100)
            {
                return BadRequest(new { error = "Limit must be between 1 and 100" });
            }

            // Build query based on provided filters
            List<Chord> chords;

            if (!string.IsNullOrWhiteSpace(search))
            {
                chords = await mongoDb.SearchChordsAsync(search, limit);
            }
            else if (!string.IsNullOrWhiteSpace(quality) && !string.IsNullOrWhiteSpace(extension))
            {
                chords = await mongoDb.GetChordsByQualityAndExtensionAsync(quality, extension, limit);
            }
            else if (!string.IsNullOrWhiteSpace(quality))
            {
                chords = await mongoDb.GetChordsByQualityAsync(quality, limit);
            }
            else if (!string.IsNullOrWhiteSpace(extension))
            {
                chords = await mongoDb.GetChordsByExtensionAsync(extension, limit);
            }
            else if (notes.HasValue)
            {
                chords = await mongoDb.GetChordsByNoteCountAsync(notes.Value, limit);
            }
            else
            {
                // No filters - return first N chords
                chords = await mongoDb.SearchChordsAsync("", limit);
            }

            return Ok(chords);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error querying chords");
            return StatusCode(500, new { error = "Error retrieving chords" });
        }
    }

    /// <summary>
    /// Get available chord qualities
    /// </summary>
    [HttpGet("qualities")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
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
            return StatusCode(500, new { error = "Error retrieving qualities" });
        }
    }

    /// <summary>
    /// Get statistics about chords in the database
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
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
            return StatusCode(500, new { error = "Error retrieving statistics" });
        }
    }
}
