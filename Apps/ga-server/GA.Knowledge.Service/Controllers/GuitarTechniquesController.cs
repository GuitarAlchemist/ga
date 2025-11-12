namespace GA.Knowledge.Service.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

using GA.Business.Core;

/// <summary>
///     API controller for guitar techniques
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GuitarTechniquesController(ILogger<GuitarTechniquesController> logger) : ControllerBase
{
    /// <summary>
    ///     Get all guitar techniques
    /// </summary>
    /// <returns>List of all guitar techniques</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<GuitarTechniqueDefinition>), 200)]
    public ActionResult<IEnumerable<GuitarTechniqueDefinition>> GetAll()
    {
        try
        {
            var techniques = GuitarTechniquesService.GetAllTechniques().ToList();
            logger.LogInformation("Retrieved {Count} guitar techniques", techniques.Count);

            return Ok(techniques);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving guitar techniques");
            return StatusCode(500, "An error occurred while retrieving guitar techniques");
        }
    }

    /// <summary>
    ///     Get a specific guitar technique by name
    /// </summary>
    /// <param name="name">Name of the guitar technique</param>
    /// <returns>The guitar technique details</returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(GuitarTechniqueDefinition), 200)]
    [ProducesResponseType(404)]
    public ActionResult<GuitarTechniqueDefinition> GetByName(string name)
    {
        try
        {
            var technique = GuitarTechniquesService.FindTechniqueByName(name);

            if (technique == null)
            {
                return NotFound($"Guitar technique '{name}' not found");
            }

            logger.LogInformation("Retrieved guitar technique '{Name}'", name);
            return Ok(technique);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving guitar technique '{Name}'", name);
            return StatusCode(500, "An error occurred while retrieving the guitar technique");
        }
    }

    /// <summary>
    ///     Get guitar techniques by category
    /// </summary>
    /// <param name="category">Category to filter by (e.g., Lead Guitar, Rhythm Guitar)</param>
    /// <returns>List of guitar techniques in the specified category</returns>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(IEnumerable<GuitarTechniqueDefinition>), 200)]
    public ActionResult<IEnumerable<GuitarTechniqueDefinition>> GetByCategory(string category)
    {
        try
        {
            var techniques = GuitarTechniquesService.FindTechniquesByCategory(category).ToList();
            logger.LogInformation("Retrieved {Count} guitar techniques for category '{Category}'", techniques.Count,
                category);

            return Ok(techniques);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving guitar techniques for category '{Category}'", category);
            return StatusCode(500, "An error occurred while retrieving guitar techniques");
        }
    }

    /// <summary>
    ///     Get guitar techniques by difficulty level
    /// </summary>
    /// <param name="difficulty">Difficulty level (e.g., Beginner, Intermediate, Advanced)</param>
    /// <returns>List of guitar techniques at the specified difficulty level</returns>
    [HttpGet("difficulty/{difficulty}")]
    [ProducesResponseType(typeof(IEnumerable<GuitarTechniqueDefinition>), 200)]
    public ActionResult<IEnumerable<GuitarTechniqueDefinition>> GetByDifficulty(string difficulty)
    {
        try
        {
            var techniques = GuitarTechniquesService.FindTechniquesByDifficulty(difficulty).ToList();
            logger.LogInformation("Retrieved {Count} guitar techniques for difficulty '{Difficulty}'", techniques.Count,
                difficulty);

            return Ok(techniques);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving guitar techniques for difficulty '{Difficulty}'", difficulty);
            return StatusCode(500, "An error occurred while retrieving guitar techniques");
        }
    }

    /// <summary>
    ///     Get guitar techniques by artist
    /// </summary>
    /// <param name="artist">Artist name</param>
    /// <returns>List of guitar techniques associated with the artist</returns>
    [HttpGet("artist/{artist}")]
    [ProducesResponseType(typeof(IEnumerable<GuitarTechniqueDefinition>), 200)]
    public ActionResult<IEnumerable<GuitarTechniqueDefinition>> GetByArtist(string artist)
    {
        try
        {
            var techniques = GuitarTechniquesService.FindTechniquesByArtist(artist).ToList();
            logger.LogInformation("Retrieved {Count} guitar techniques for artist '{Artist}'", techniques.Count,
                artist);

            return Ok(techniques);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving guitar techniques for artist '{Artist}'", artist);
            return StatusCode(500, "An error occurred while retrieving guitar techniques");
        }
    }

    /// <summary>
    ///     Get guitar techniques by inventor
    /// </summary>
    /// <param name="inventor">Inventor/developer of the technique</param>
    /// <returns>List of guitar techniques developed by the inventor</returns>
    [HttpGet("inventor/{inventor}")]
    [ProducesResponseType(typeof(IEnumerable<GuitarTechniqueDefinition>), 200)]
    public ActionResult<IEnumerable<GuitarTechniqueDefinition>> GetByInventor(string inventor)
    {
        try
        {
            var techniques = GuitarTechniquesService.FindTechniquesByInventor(inventor).ToList();
            logger.LogInformation("Retrieved {Count} guitar techniques for inventor '{Inventor}'", techniques.Count,
                inventor);

            return Ok(techniques);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving guitar techniques for inventor '{Inventor}'", inventor);
            return StatusCode(500, "An error occurred while retrieving guitar techniques");
        }
    }

    /// <summary>
    ///     Search guitar techniques by song title
    /// </summary>
    /// <param name="song">Song title to search for</param>
    /// <returns>List of guitar techniques used in songs matching the title</returns>
    [HttpGet("song/{song}")]
    [ProducesResponseType(typeof(IEnumerable<GuitarTechniqueDefinition>), 200)]
    public ActionResult<IEnumerable<GuitarTechniqueDefinition>> GetBySong(string song)
    {
        try
        {
            var techniques = GuitarTechniquesService.FindTechniquesBySong(song).ToList();
            logger.LogInformation("Retrieved {Count} guitar techniques for song '{Song}'", techniques.Count, song);

            return Ok(techniques);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving guitar techniques for song '{Song}'", song);
            return StatusCode(500, "An error occurred while retrieving guitar techniques");
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
            var categories = GuitarTechniquesService.GetAllCategories().ToList();
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
    ///     Get all unique difficulty levels
    /// </summary>
    /// <returns>List of all unique difficulty levels</returns>
    [HttpGet("difficulties")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetAllDifficulties()
    {
        try
        {
            var difficulties = GuitarTechniquesService.GetAllDifficulties().ToList();
            logger.LogInformation("Retrieved {Count} unique difficulties", difficulties.Count);

            return Ok(difficulties);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving difficulties");
            return StatusCode(500, "An error occurred while retrieving difficulties");
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
            var artists = GuitarTechniquesService.GetAllArtists().ToList();
            logger.LogInformation("Retrieved {Count} unique artists", artists.Count);

            return Ok(artists);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving artists");
            return StatusCode(500, "An error occurred while retrieving artists");
        }
    }

    /// <summary>
    ///     Get all unique inventors
    /// </summary>
    /// <returns>List of all unique inventors</returns>
    [HttpGet("inventors")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetAllInventors()
    {
        try
        {
            var inventors = GuitarTechniquesService.GetAllInventors().ToList();
            logger.LogInformation("Retrieved {Count} unique inventors", inventors.Count);

            return Ok(inventors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving inventors");
            return StatusCode(500, "An error occurred while retrieving inventors");
        }
    }
}
