namespace GA.MusicTheory.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using GA.MusicTheory.Service.Services;
using GA.MusicTheory.Service.Models;
using MongoDB.Driver;
using AllProjects.ServiceDefaults;

/// <summary>
///     API controller for direct database inspection and raw collection access
/// </summary>
[ApiController]
[Route("api/database")]
[Produces("application/json")]
public class DatabaseController(MongoDbService mongoService, ILogger<DatabaseController> logger) : ControllerBase
{
    /// <summary>
    ///     Get chords collection with pagination and filtering
    /// </summary>
    [HttpGet("chords")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<Chord>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetChords([FromQuery] PaginationRequest request)
    {
        try
        {
            var filter = Builders<Chord>.Filter.Empty;
            var total = await mongoService.Chords.CountDocumentsAsync(filter);
            
            var items = await mongoService.Chords.Find(filter)
                .Skip((request.Page - 1) * request.PageSize)
                .Limit(request.PageSize)
                .ToListAsync();

            return Ok(ApiResponse<IEnumerable<Chord>>.Ok(
                items,
                pagination: new PaginationInfo
                {
                    Page = request.Page,
                    PageSize = request.PageSize,
                    TotalItems = total,
                    ItemCount = items.Count
                }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving chords from database");
            return StatusCode(500, ApiResponse<object>.Fail("Failed to retrieve chords", ex.Message));
        }
    }

    /// <summary>
    ///     Get server statistics and collection info
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var collections = await mongoService.Database.ListCollectionNames().ToListAsync();
            var stats = new Dictionary<string, object>();
            
            foreach (var collName in collections)
            {
                var count = await mongoService.Database.GetCollection<object>(collName).CountDocumentsAsync(Builders<object>.Filter.Empty);
                stats[collName] = count;
            }

            return Ok(ApiResponse<object>.Ok(new
            {
                DatabaseName = mongoService.Database.DatabaseNamespace.DatabaseName,
                Collections = stats,
                Timestamp = DateTime.UtcNow
            }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting database stats");
            return StatusCode(500, ApiResponse<object>.Fail("Failed to get database stats", ex.Message));
        }
    }
}
