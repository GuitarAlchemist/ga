namespace GA.Fretboard.Service.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

using GA.Fretboard.Service.Models;
using GA.Fretboard.Service.Services;

/// <summary>
///     Controller demonstrating monadic service integration with type-safe error handling
/// </summary>
[ApiController]
[Route("api/monadic/chords")]
[Produces("application/json")]
public class MonadicChordsController : ControllerBase
{
    private readonly ILogger<MonadicChordsController> _logger;
    private readonly MonadicChordService _monadicChordService;

    public MonadicChordsController(
        MonadicChordService monadicChordService,
        ILogger<MonadicChordsController> logger)
    {
        _monadicChordService = monadicChordService;
        _logger = logger;
    }

    /// <summary>
    ///     Get total count of chords using Try monad
    /// </summary>
    /// <returns>Total count or error</returns>
    [HttpGet("count")]
    [ProducesResponseType(typeof(long), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetTotalCount()
    {
        try
        {
            var count = await _monadicChordService.GetTotalCountAsync();
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get total chord count");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to retrieve chord count",
                Details = ex.Message
            });
        }
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
        try
        {
            var chord = await _monadicChordService.GetByIdAsync(id);
            if (chord == null)
                return NotFound(new { message = $"Chord with ID {id} not found" });
            return Ok(chord);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chord by ID {Id}", id);
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to retrieve chord"
            });
        }
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
        try
        {
            var result = await _monadicChordService.GetByQualityAsync(quality, new { limit });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chords by quality {Quality}", quality);
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to retrieve chords by quality",
                Details = ex.Message
            });
        }
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
        var result = await _monadicChordService.GetByExtensionAsync(extension, limit);

        // Handle the result directly since service returns List<object>
        if (result != null && result.Any())
        {
            return Ok(result);
        }

        return NotFound($"No chords found with extension: {extension}");
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
        try
        {
            var result = await _monadicChordService.GetByStackingTypeAsync(stackingType, new { limit });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chords by stacking type {StackingType}", stackingType);
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to retrieve chords by stacking type",
                Details = ex.Message
            });
        }
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
        try
        {
            var result = await _monadicChordService.SearchChordsAsync(query, new { limit });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search chords with query {Query}", query);
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to search chords",
                Details = ex.Message
            });
        }
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
        try
        {
            var result = await _monadicChordService.GetSimilarChordsAsync(id, new { limit });
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get similar chords for {Id}", id);
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to retrieve similar chords",
                Details = ex.Message
            });
        }
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
        try
        {
            var stats = await _monadicChordService.GetStatisticsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get chord statistics");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                    Message = "Failed to retrieve chord statistics",
                    Details = ex.Message
                });
            }
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
        try
        {
            var qualities = await _monadicChordService.GetAvailableQualitiesAsync();
            return Ok(qualities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get available qualities");
            return StatusCode(500, new ErrorResponse
            {
                Error = "InternalServerError",
                Message = "Failed to retrieve available qualities",
                Details = ex.Message
            });
        }
    }

    // Helper method to map ChordError to IActionResult
    private IActionResult MapChordErrorToResponse(ChordError error)
    {
        return error.Type switch
        {
            ChordErrorType.ValidationError => BadRequest(new ErrorResponse
            {
                Error = "ValidationError",
                Message = error.Message
            }),
            ChordErrorType.NotFound => NotFound(new ErrorResponse
            {
                Error = "NotFound",
                Message = error.Message
            }),
            ChordErrorType.DatabaseError => StatusCode(500, new ErrorResponse
            {
                Error = "DatabaseError",
                Message = "Failed to retrieve chords from database",
                Details = error.Message
            }),
            _ => StatusCode(500, new ErrorResponse
            {
                Error = "UnknownError",
                Message = error.Message
            })
        };
    }
}

/// <summary>
///     Standard error response model
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
}
