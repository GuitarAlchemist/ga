namespace GA.MusicTheory.Service.Controllers;

using Services;
using Models;
using Microsoft.AspNetCore.Mvc;
using ErrorResponse = GA.MusicTheory.Service.Models.ErrorResponse;
using AllProjects.ServiceDefaults;

/// <summary>
///     Controller demonstrating monadic service integration with type-safe error handling
/// </summary>
[ApiController]
[Route("api/monadic/chords")]
[Produces("application/json")]
public class MonadicChordsController(
    IMonadicChordService monadicChordService,
    ILogger<MonadicChordsController> logger) : ControllerBase
{
    /// <summary>
    ///     Get total count of chords using Try monad
    /// </summary>
    /// <returns>Total count or error</returns>
    [HttpGet("count")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTotalCount()
    {
        var tryCount = await monadicChordService.GetTotalCountAsync();

        return tryCount.Match<IActionResult>(
            onSuccess: count => Ok(new { count }),
            onFailure: ex =>
            {
                logger.LogError(ex, "Failed to get total chord count");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "Failed to retrieve chord count",
                    Details = ex.Message
                });
            }
        );
    }

    /// <summary>
    ///     Get chord by ID using Option monad
    /// </summary>
    /// <param name="id">Chord ID</param>
    /// <returns>Chord if found, 404 if not found</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Chord), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var chordOption = await monadicChordService.GetByIdAsync(id);

        return chordOption.Match<IActionResult>(
            chord => Ok(chord),
            () => NotFound(new { message = $"Chord with ID {id} not found" })
        );
    }

    /// <summary>
    ///     Get chords by quality using Result monad with explicit error handling
    /// </summary>
    /// <param name="quality">Chord quality (e.g., Major, Minor, Dominant)</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of chords or error details</returns>
    [HttpGet("quality/{quality}")]
    [ProducesResponseType(typeof(List<Chord>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByQuality(string quality, [FromQuery] int limit = 100)
    {
        var result = await monadicChordService.GetByQualityAsync(quality, limit);

        return result.Match<IActionResult>(
            chords => Ok(chords),
            error => MapChordErrorToResponse(error)
        );
    }

    /// <summary>
    ///     Get chords by extension using Result monad
    /// </summary>
    /// <param name="extension">Chord extension (e.g., 7th, 9th, 11th)</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of chords or error details</returns>
    [HttpGet("extension/{extension}")]
    [ProducesResponseType(typeof(List<Chord>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByExtension(string extension, [FromQuery] int limit = 100)
    {
        var result = await monadicChordService.GetByExtensionAsync(extension, limit);

        return result.Match<IActionResult>(
            chords => Ok(chords),
            error => MapChordErrorToResponse(error)
        );
    }

    /// <summary>
    ///     Get chords by stacking type using Result monad
    /// </summary>
    /// <param name="stackingType">Stacking type (e.g., Tertian, Quartal, Quintal)</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of chords or error details</returns>
    [HttpGet("stacking/{stackingType}")]
    [ProducesResponseType(typeof(List<Chord>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetByStackingType(string stackingType, [FromQuery] int limit = 100)
    {
        var result = await monadicChordService.GetByStackingTypeAsync(stackingType, limit);

        return result.Match<IActionResult>(
            chords => Ok(chords),
            error => MapChordErrorToResponse(error)
        );
    }

    /// <summary>
    ///     Search chords using Result monad
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of chords or error details</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<Chord>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int limit = 100)
    {
        var result = await monadicChordService.SearchChordsAsync(query, limit);

        return result.Match<IActionResult>(
            chords => Ok(chords),
            error => MapChordErrorToResponse(error)
        );
    }

    /// <summary>
    ///     Get similar chords using Result monad
    /// </summary>
    /// <param name="id">Chord ID</param>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of similar chords or error details</returns>
    [HttpGet("{id}/similar")]
    [ProducesResponseType(typeof(List<Chord>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetSimilar(string id, [FromQuery] int limit = 10)
    {
        var result = await monadicChordService.GetSimilarChordsAsync(id, limit);

        return result.Match<IActionResult>(
            chords => Ok(chords),
            error => error.Type switch
            {
                ChordErrorType.NotFound => NotFound(new ErrorResponse
                {
                    Error = "NotFound",
                    Message = $"Chord with ID {id} not found"
                }),
                _ => MapChordErrorToResponse(error)
            }
        );
    }

    /// <summary>
    ///     Get chord statistics using Try monad
    /// </summary>
    /// <returns>Chord statistics or error</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ChordStatistics), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStatistics()
    {
        var tryStats = await monadicChordService.GetStatisticsAsync();

        return tryStats.Match<IActionResult>(
            onSuccess: stats => Ok(stats),
            onFailure: ex =>
            {
                logger.LogError(ex, "Failed to get chord statistics");
                return StatusCode(500, new ErrorResponse
                {
                    Error = "InternalServerError",
                    Message = "Failed to retrieve chord statistics",
                    Details = ex.Message
                });
            }
        );
    }

    /// <summary>
    ///     Get available chord qualities using Try monad
    /// </summary>
    /// <returns>List of available qualities or error</returns>
    [HttpGet("qualities")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAvailableQualities()
    {
        var tryQualities = await monadicChordService.GetAvailableQualitiesAsync();

        return tryQualities.Match<IActionResult>(
            onSuccess: qualities => Ok(qualities),
            onFailure: ex => StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to retrieve available qualities",
                Details = ex.Message
            })
        );
    }

    /// <summary>
    ///     Map a ChordError to the appropriate HTTP response
    /// </summary>
    private static IActionResult MapChordErrorToResponse(ChordError error) =>
        error.Type switch
        {
            ChordErrorType.ValidationError => new BadRequestObjectResult(new ErrorResponse
            {
                Error = "ValidationError",
                Message = error.Message
            }),
            ChordErrorType.NotFound => new NotFoundObjectResult(new ErrorResponse
            {
                Error = "NotFound",
                Message = error.Message
            }),
            ChordErrorType.DatabaseError => new ObjectResult(new ErrorResponse
            {
                Error = "DatabaseError",
                Message = "Failed to retrieve chords from database",
                Details = error.Message
            }) { StatusCode = 500 },
            _ => new ObjectResult(new ErrorResponse
            {
                Error = "UnknownError",
                Message = error.Message
            }) { StatusCode = 500 }
        };
}
