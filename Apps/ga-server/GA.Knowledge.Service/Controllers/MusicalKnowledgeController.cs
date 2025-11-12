namespace GA.Knowledge.Service.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

using System.ComponentModel.DataAnnotations;
using GA.Business.Core;

/// <summary>
///     API controller for accessing musical knowledge from YAML configurations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MusicalKnowledgeController(ILogger<MusicalKnowledgeController> logger) : ControllerBase
{
    /// <summary>
    ///     Search across all musical concepts
    /// </summary>
    /// <param name="query">Search term to find across all musical concepts</param>
    /// <returns>Search results containing matching chords, progressions, techniques, and tunings</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(MusicalKnowledgeSearchResult), 200)]
    [ProducesResponseType(400)]
    public ActionResult<MusicalKnowledgeSearchResult> Search([FromQuery] [Required] string query)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest("Search query cannot be empty");
            }

            var result = MusicalKnowledgeService.SearchAll(query);
            logger.LogInformation("Search for '{Query}' returned {TotalResults} results", query, result.TotalResults);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching for '{Query}'", query);
            return StatusCode(500, "An error occurred while searching");
        }
    }

    /// <summary>
    ///     Get musical knowledge statistics
    /// </summary>
    /// <returns>Comprehensive statistics about the musical knowledge base</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(MusicalKnowledgeStatistics), 200)]
    public ActionResult<MusicalKnowledgeStatistics> GetStatistics()
    {
        try
        {
            var stats = MusicalKnowledgeService.GetStatistics();
            logger.LogInformation("Retrieved statistics: {TotalConcepts} total concepts", stats.TotalConcepts);

            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving statistics");
            return StatusCode(500, "An error occurred while retrieving statistics");
        }
    }

    /// <summary>
    ///     Get all musical concepts by category
    /// </summary>
    /// <param name="category">Category to filter by (e.g., Jazz, Classical, Rock)</param>
    /// <returns>All musical concepts in the specified category</returns>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(MusicalKnowledgeByCategory), 200)]
    [ProducesResponseType(404)]
    public ActionResult<MusicalKnowledgeByCategory> GetByCategory(string category)
    {
        try
        {
            var result = MusicalKnowledgeService.GetByCategory(category);

            var totalItems = result.IconicChords.Count + result.ChordProgressions.Count +
                             result.GuitarTechniques.Count + result.SpecializedTunings.Count;

            if (totalItems == 0)
            {
                return NotFound($"No musical concepts found for category '{category}'");
            }

            logger.LogInformation("Retrieved {TotalItems} items for category '{Category}'", totalItems, category);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving category '{Category}'", category);
            return StatusCode(500, "An error occurred while retrieving category data");
        }
    }

    /// <summary>
    ///     Get all musical concepts by difficulty level
    /// </summary>
    /// <param name="difficulty">Difficulty level (e.g., Beginner, Intermediate, Advanced)</param>
    /// <returns>All musical concepts at the specified difficulty level</returns>
    [HttpGet("difficulty/{difficulty}")]
    [ProducesResponseType(typeof(MusicalKnowledgeByDifficulty), 200)]
    [ProducesResponseType(404)]
    public ActionResult<MusicalKnowledgeByDifficulty> GetByDifficulty(string difficulty)
    {
        try
        {
            var result = MusicalKnowledgeService.GetByDifficulty(difficulty);

            var totalItems = result.ChordProgressions.Count + result.GuitarTechniques.Count;

            if (totalItems == 0)
            {
                return NotFound($"No musical concepts found for difficulty '{difficulty}'");
            }

            logger.LogInformation("Retrieved {TotalItems} items for difficulty '{Difficulty}'", totalItems, difficulty);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving difficulty '{Difficulty}'", difficulty);
            return StatusCode(500, "An error occurred while retrieving difficulty data");
        }
    }

    /// <summary>
    ///     Get all musical concepts associated with an artist
    /// </summary>
    /// <param name="artist">Artist name to search for</param>
    /// <returns>All musical concepts associated with the specified artist</returns>
    [HttpGet("artist/{artist}")]
    [ProducesResponseType(typeof(MusicalKnowledgeByArtist), 200)]
    [ProducesResponseType(404)]
    public ActionResult<MusicalKnowledgeByArtist> GetByArtist(string artist)
    {
        try
        {
            var result = MusicalKnowledgeService.GetByArtist(artist);

            var totalItems = result.IconicChords.Count + result.ChordProgressions.Count +
                             result.GuitarTechniques.Count + result.SpecializedTunings.Count;

            if (totalItems == 0)
            {
                return NotFound($"No musical concepts found for artist '{artist}'");
            }

            logger.LogInformation("Retrieved {TotalItems} items for artist '{Artist}'", totalItems, artist);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving artist '{Artist}'", artist);
            return StatusCode(500, "An error occurred while retrieving artist data");
        }
    }

    /// <summary>
    ///     Validate all YAML configurations
    /// </summary>
    /// <returns>Validation results for all configurations</returns>
    [HttpGet("validate")]
    [ProducesResponseType(typeof(MusicalKnowledgeValidationResult), 200)]
    public ActionResult<MusicalKnowledgeValidationResult> ValidateConfigurations()
    {
        try
        {
            var validation = MusicalKnowledgeService.ValidateAll();

            if (!validation.IsValid)
            {
                logger.LogWarning("Configuration validation failed with {ErrorCount} errors",
                    validation.AllErrors.Count);
            }
            else
            {
                logger.LogInformation("All configurations validated successfully");
            }

            return Ok(validation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating configurations");
            return StatusCode(500, "An error occurred while validating configurations");
        }
    }

    /// <summary>
    ///     Get all unique artists across all configurations
    /// </summary>
    /// <returns>List of all unique artists</returns>
    [HttpGet("artists")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetAllArtists()
    {
        try
        {
            var artists = MusicalKnowledgeService.GetAllArtists().ToList();
            logger.LogInformation("Retrieved {ArtistCount} unique artists", artists.Count);

            return Ok(artists);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving artists");
            return StatusCode(500, "An error occurred while retrieving artists");
        }
    }

    /// <summary>
    ///     Get all unique categories across all configurations
    /// </summary>
    /// <returns>List of all unique categories</returns>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetAllCategories()
    {
        try
        {
            var categories = MusicalKnowledgeService.GetAllCategories().ToList();
            logger.LogInformation("Retrieved {CategoryCount} unique categories", categories.Count);

            return Ok(categories);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, "An error occurred while retrieving categories");
        }
    }

    /// <summary>
    ///     Get all unique difficulty levels across all configurations
    /// </summary>
    /// <returns>List of all unique difficulty levels</returns>
    [HttpGet("difficulties")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetAllDifficulties()
    {
        try
        {
            var difficulties = MusicalKnowledgeService.GetAllDifficulties().ToList();
            logger.LogInformation("Retrieved {DifficultyCount} unique difficulties", difficulties.Count);

            return Ok(difficulties);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving difficulties");
            return StatusCode(500, "An error occurred while retrieving difficulties");
        }
    }

    /// <summary>
    ///     Reload all YAML configurations
    /// </summary>
    /// <returns>Success message</returns>
    [HttpPost("reload")]
    [ProducesResponseType(typeof(object), 200)]
    public ActionResult ReloadConfigurations()
    {
        try
        {
            MusicalKnowledgeService.ReloadAllConfigurations();
            logger.LogInformation("All configurations reloaded successfully");

            return Ok(new { message = "All configurations reloaded successfully", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reloading configurations");
            return StatusCode(500, "An error occurred while reloading configurations");
        }
    }
}
