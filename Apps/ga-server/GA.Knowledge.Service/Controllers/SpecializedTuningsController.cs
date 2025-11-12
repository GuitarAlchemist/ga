namespace GA.Knowledge.Service.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

using System.ComponentModel.DataAnnotations;
using GA.Business.Core;

/// <summary>
///     API controller for specialized tunings
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SpecializedTuningsController(ILogger<SpecializedTuningsController> logger) : ControllerBase
{
    /// <summary>
    ///     Get all specialized tunings
    /// </summary>
    /// <returns>List of all specialized tunings</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SpecializedTuningDefinition>), 200)]
    public ActionResult<IEnumerable<SpecializedTuningDefinition>> GetAll()
    {
        try
        {
            var tunings = SpecializedTuningsService.GetAllTunings().ToList();
            logger.LogInformation("Retrieved {Count} specialized tunings", tunings.Count);

            return Ok(tunings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving specialized tunings");
            return StatusCode(500, "An error occurred while retrieving specialized tunings");
        }
    }

    /// <summary>
    ///     Get a specific specialized tuning by name
    /// </summary>
    /// <param name="name">Name of the specialized tuning</param>
    /// <returns>The specialized tuning details</returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(SpecializedTuningDefinition), 200)]
    [ProducesResponseType(404)]
    public ActionResult<SpecializedTuningDefinition> GetByName(string name)
    {
        try
        {
            var tuning = SpecializedTuningsService.FindTuningByName(name);

            if (tuning == null)
            {
                return NotFound($"Specialized tuning '{name}' not found");
            }

            logger.LogInformation("Retrieved specialized tuning '{Name}'", name);
            return Ok(tuning);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving specialized tuning '{Name}'", name);
            return StatusCode(500, "An error occurred while retrieving the specialized tuning");
        }
    }

    /// <summary>
    ///     Get alternative string configurations
    /// </summary>
    /// <returns>List of alternative string configurations</returns>
    [HttpGet("alternative-configurations")]
    [ProducesResponseType(typeof(IEnumerable<SpecializedTuningDefinition>), 200)]
    public ActionResult<IEnumerable<SpecializedTuningDefinition>> GetAlternativeConfigurations()
    {
        try
        {
            var tunings = SpecializedTuningsService.GetAlternativeStringConfigurations().ToList();
            logger.LogInformation("Retrieved {Count} alternative string configurations", tunings.Count);

            return Ok(tunings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving alternative string configurations");
            return StatusCode(500, "An error occurred while retrieving alternative string configurations");
        }
    }

    /// <summary>
    ///     Get specialized tuning systems
    /// </summary>
    /// <returns>List of specialized tuning systems</returns>
    [HttpGet("tuning-systems")]
    [ProducesResponseType(typeof(IEnumerable<SpecializedTuningDefinition>), 200)]
    public ActionResult<IEnumerable<SpecializedTuningDefinition>> GetTuningSystems()
    {
        try
        {
            var tunings = SpecializedTuningsService.GetSpecializedTuningSystems().ToList();
            logger.LogInformation("Retrieved {Count} specialized tuning systems", tunings.Count);

            return Ok(tunings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving specialized tuning systems");
            return StatusCode(500, "An error occurred while retrieving specialized tuning systems");
        }
    }

    /// <summary>
    ///     Get extended range instruments
    /// </summary>
    /// <returns>List of extended range instrument tunings</returns>
    [HttpGet("extended-range")]
    [ProducesResponseType(typeof(IEnumerable<SpecializedTuningDefinition>), 200)]
    public ActionResult<IEnumerable<SpecializedTuningDefinition>> GetExtendedRange()
    {
        try
        {
            var tunings = SpecializedTuningsService.GetExtendedRangeInstruments().ToList();
            logger.LogInformation("Retrieved {Count} extended range instrument tunings", tunings.Count);

            return Ok(tunings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving extended range instrument tunings");
            return StatusCode(500, "An error occurred while retrieving extended range instrument tunings");
        }
    }

    /// <summary>
    ///     Get recording tunings
    /// </summary>
    /// <returns>List of recording and production tunings</returns>
    [HttpGet("recording")]
    [ProducesResponseType(typeof(IEnumerable<SpecializedTuningDefinition>), 200)]
    public ActionResult<IEnumerable<SpecializedTuningDefinition>> GetRecordingTunings()
    {
        try
        {
            var tunings = SpecializedTuningsService.GetRecordingTunings().ToList();
            logger.LogInformation("Retrieved {Count} recording tunings", tunings.Count);

            return Ok(tunings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving recording tunings");
            return StatusCode(500, "An error occurred while retrieving recording tunings");
        }
    }

    /// <summary>
    ///     Get specialized tunings by category
    /// </summary>
    /// <param name="category">Category to filter by (e.g., Studio Technique, Extended Range)</param>
    /// <returns>List of specialized tunings in the specified category</returns>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(IEnumerable<SpecializedTuningDefinition>), 200)]
    public ActionResult<IEnumerable<SpecializedTuningDefinition>> GetByCategory(string category)
    {
        try
        {
            var tunings = SpecializedTuningsService.FindTuningsByCategory(category).ToList();
            logger.LogInformation("Retrieved {Count} specialized tunings for category '{Category}'", tunings.Count,
                category);

            return Ok(tunings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving specialized tunings for category '{Category}'", category);
            return StatusCode(500, "An error occurred while retrieving specialized tunings");
        }
    }

    /// <summary>
    ///     Get specialized tunings by application
    /// </summary>
    /// <param name="application">Application to filter by (e.g., Folk music, Metal, Jazz)</param>
    /// <returns>List of specialized tunings for the specified application</returns>
    [HttpGet("application/{application}")]
    [ProducesResponseType(typeof(IEnumerable<SpecializedTuningDefinition>), 200)]
    public ActionResult<IEnumerable<SpecializedTuningDefinition>> GetByApplication(string application)
    {
        try
        {
            var tunings = SpecializedTuningsService.FindTuningsByApplication(application).ToList();
            logger.LogInformation("Retrieved {Count} specialized tunings for application '{Application}'",
                tunings.Count, application);

            return Ok(tunings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving specialized tunings for application '{Application}'", application);
            return StatusCode(500, "An error occurred while retrieving specialized tunings");
        }
    }

    /// <summary>
    ///     Get specialized tunings by artist
    /// </summary>
    /// <param name="artist">Artist name</param>
    /// <returns>List of specialized tunings associated with the artist</returns>
    [HttpGet("artist/{artist}")]
    [ProducesResponseType(typeof(IEnumerable<SpecializedTuningDefinition>), 200)]
    public ActionResult<IEnumerable<SpecializedTuningDefinition>> GetByArtist(string artist)
    {
        try
        {
            var tunings = SpecializedTuningsService.FindTuningsByArtist(artist).ToList();
            logger.LogInformation("Retrieved {Count} specialized tunings for artist '{Artist}'", tunings.Count, artist);

            return Ok(tunings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving specialized tunings for artist '{Artist}'", artist);
            return StatusCode(500, "An error occurred while retrieving specialized tunings");
        }
    }

    /// <summary>
    ///     Find specialized tunings by pitch classes
    /// </summary>
    /// <param name="pitchClasses">Comma-separated pitch class numbers (0-11)</param>
    /// <returns>List of specialized tunings matching the pitch classes</returns>
    [HttpGet("pitch-classes")]
    [ProducesResponseType(typeof(IEnumerable<SpecializedTuningDefinition>), 200)]
    [ProducesResponseType(400)]
    public ActionResult<IEnumerable<SpecializedTuningDefinition>> GetByPitchClasses(
        [FromQuery] [Required] string pitchClasses)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(pitchClasses))
            {
                return BadRequest("Pitch classes parameter cannot be empty");
            }

            var pitchClassList = new List<int>();
            var pitchClassStrings = pitchClasses.Split(',', StringSplitOptions.RemoveEmptyEntries);

            foreach (var pcString in pitchClassStrings)
            {
                if (int.TryParse(pcString.Trim(), out var pc) && pc >= 0 && pc <= 11)
                {
                    pitchClassList.Add(pc);
                }
                else
                {
                    return BadRequest($"Invalid pitch class '{pcString.Trim()}'. Must be integer 0-11.");
                }
            }

            if (!pitchClassList.Any())
            {
                return BadRequest("No valid pitch classes provided");
            }

            var tunings = SpecializedTuningsService.FindTuningsByPitchClasses(pitchClassList).ToList();
            logger.LogInformation("Retrieved {Count} specialized tunings for pitch classes '{PitchClasses}'",
                tunings.Count, pitchClasses);

            return Ok(tunings);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving specialized tunings for pitch classes '{PitchClasses}'",
                pitchClasses);
            return StatusCode(500, "An error occurred while retrieving specialized tunings");
        }
    }

    /// <summary>
    ///     Get all unique categories
    /// </summary>
    /// <returns>List of all unique categories</returns>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetAllCategories()
    {
        try
        {
            var categories = SpecializedTuningsService.GetAllCategories().ToList();
            logger.LogInformation("Retrieved {Count} unique categories", categories.Count);

            return Ok(categories);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, "An error occurred while retrieving categories");
        }
    }

    /// <summary>
    ///     Get all unique applications
    /// </summary>
    /// <returns>List of all unique applications</returns>
    [HttpGet("applications")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetAllApplications()
    {
        try
        {
            var applications = SpecializedTuningsService.GetAllApplications().ToList();
            logger.LogInformation("Retrieved {Count} unique applications", applications.Count);

            return Ok(applications);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving applications");
            return StatusCode(500, "An error occurred while retrieving applications");
        }
    }

    /// <summary>
    ///     Get all unique artists
    /// </summary>
    /// <returns>List of all unique artists</returns>
    [HttpGet("artists")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetAllArtists()
    {
        try
        {
            var artists = SpecializedTuningsService.GetAllArtists().ToList();
            logger.LogInformation("Retrieved {Count} unique artists", artists.Count);

            return Ok(artists);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving artists");
            return StatusCode(500, "An error occurred while retrieving artists");
        }
    }
}
