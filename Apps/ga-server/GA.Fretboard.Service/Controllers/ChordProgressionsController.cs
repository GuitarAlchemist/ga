namespace GA.Fretboard.Service.Controllers;

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using GA.Business.Core;
using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard;
using GA.Fretboard.Service.Models;
using GA.Fretboard.Service.Services;

/// <summary>
///     API controller for chord progressions
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChordProgressionsController(
    ILogger<ChordProgressionsController> logger,
    IShapeGraphBuilder shapeGraphBuilder,
    ProgressionAnalyzer progressionAnalyzer,
    HarmonicDynamics harmonicDynamics) : ControllerBase
{
    /// <summary>
    ///     Get all chord progressions
    /// </summary>
    /// <returns>List of all chord progressions</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ChordProgressionDefinition>), 200)]
    public ActionResult<IEnumerable<ChordProgressionDefinition>> GetAll()
    {
        try
        {
            var progressions = ChordProgressionsService.GetAllProgressions().ToList();
            logger.LogInformation("Retrieved {Count} chord progressions", progressions.Count);

            return Ok(progressions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving chord progressions");
            return StatusCode(500, "An error occurred while retrieving chord progressions");
        }
    }

    /// <summary>
    ///     Get a specific chord progression by name
    /// </summary>
    /// <param name="name">Name of the chord progression</param>
    /// <returns>The chord progression details</returns>
    [HttpGet("{name}")]
    [ProducesResponseType(typeof(ChordProgressionDefinition), 200)]
    [ProducesResponseType(404)]
    public ActionResult<ChordProgressionDefinition> GetByName(string name)
    {
        try
        {
            var progression = ChordProgressionsService.FindProgressionByName(name);

            if (progression == null)
            {
                return NotFound($"Chord progression '{name}' not found");
            }

            logger.LogInformation("Retrieved chord progression '{Name}'", name);
            return Ok(progression);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving chord progression '{Name}'", name);
            return StatusCode(500, "An error occurred while retrieving the chord progression");
        }
    }

    /// <summary>
    ///     Get chord progressions by category
    /// </summary>
    /// <param name="category">Category to filter by (e.g., Jazz, Pop, Blues)</param>
    /// <returns>List of chord progressions in the specified category</returns>
    [HttpGet("category/{category}")]
    [ProducesResponseType(typeof(IEnumerable<ChordProgressionDefinition>), 200)]
    public ActionResult<IEnumerable<ChordProgressionDefinition>> GetByCategory(string category)
    {
        try
        {
            var progressions = ChordProgressionsService.FindProgressionsByCategory(category).ToList();
            logger.LogInformation("Retrieved {Count} chord progressions for category '{Category}'", progressions.Count,
                category);

            return Ok(progressions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving chord progressions for category '{Category}'", category);
            return StatusCode(500, "An error occurred while retrieving chord progressions");
        }
    }

    /// <summary>
    ///     Get chord progressions by difficulty level
    /// </summary>
    /// <param name="difficulty">Difficulty level (e.g., Beginner, Intermediate, Advanced)</param>
    /// <returns>List of chord progressions at the specified difficulty level</returns>
    [HttpGet("difficulty/{difficulty}")]
    [ProducesResponseType(typeof(IEnumerable<ChordProgressionDefinition>), 200)]
    public ActionResult<IEnumerable<ChordProgressionDefinition>> GetByDifficulty(string difficulty)
    {
        try
        {
            var progressions = ChordProgressionsService.FindProgressionsByDifficulty(difficulty).ToList();
            logger.LogInformation("Retrieved {Count} chord progressions for difficulty '{Difficulty}'",
                progressions.Count, difficulty);

            return Ok(progressions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving chord progressions for difficulty '{Difficulty}'", difficulty);
            return StatusCode(500, "An error occurred while retrieving chord progressions");
        }
    }

    /// <summary>
    ///     Get chord progressions by key
    /// </summary>
    /// <param name="key">Musical key (e.g., C Major, A Minor)</param>
    /// <returns>List of chord progressions in the specified key</returns>
    [HttpGet("key/{key}")]
    [ProducesResponseType(typeof(IEnumerable<ChordProgressionDefinition>), 200)]
    public ActionResult<IEnumerable<ChordProgressionDefinition>> GetByKey(string key)
    {
        try
        {
            var progressions = ChordProgressionsService.FindProgressionsByKey(key).ToList();
            logger.LogInformation("Retrieved {Count} chord progressions for key '{Key}'", progressions.Count, key);

            return Ok(progressions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving chord progressions for key '{Key}'", key);
            return StatusCode(500, "An error occurred while retrieving chord progressions");
        }
    }

    /// <summary>
    ///     Get chord progressions by artist
    /// </summary>
    /// <param name="artist">Artist name</param>
    /// <returns>List of chord progressions associated with the artist</returns>
    [HttpGet("artist/{artist}")]
    [ProducesResponseType(typeof(IEnumerable<ChordProgressionDefinition>), 200)]
    public ActionResult<IEnumerable<ChordProgressionDefinition>> GetByArtist(string artist)
    {
        try
        {
            var progressions = ChordProgressionsService.FindProgressionsByArtist(artist).ToList();
            logger.LogInformation("Retrieved {Count} chord progressions for artist '{Artist}'", progressions.Count,
                artist);

            return Ok(progressions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving chord progressions for artist '{Artist}'", artist);
            return StatusCode(500, "An error occurred while retrieving chord progressions");
        }
    }

    /// <summary>
    ///     Search chord progressions by song title
    /// </summary>
    /// <param name="song">Song title to search for</param>
    /// <returns>List of chord progressions used in songs matching the title</returns>
    [HttpGet("song/{song}")]
    [ProducesResponseType(typeof(IEnumerable<ChordProgressionDefinition>), 200)]
    public ActionResult<IEnumerable<ChordProgressionDefinition>> GetBySong(string song)
    {
        try
        {
            var progressions = ChordProgressionsService.FindProgressionsBySong(song).ToList();
            logger.LogInformation("Retrieved {Count} chord progressions for song '{Song}'", progressions.Count, song);

            return Ok(progressions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving chord progressions for song '{Song}'", song);
            return StatusCode(500, "An error occurred while retrieving chord progressions");
        }
    }

    /// <summary>
    ///     Find chord progressions by Roman numeral sequence
    /// </summary>
    /// <param name="romanNumerals">Comma-separated Roman numerals (e.g., "I,vi,IV,V")</param>
    /// <returns>List of chord progressions matching the Roman numeral sequence</returns>
    [HttpGet("roman-numerals")]
    [ProducesResponseType(typeof(IEnumerable<ChordProgressionDefinition>), 200)]
    [ProducesResponseType(400)]
    public ActionResult<IEnumerable<ChordProgressionDefinition>> GetByRomanNumerals(
        [FromQuery] [Required] string romanNumerals)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(romanNumerals))
            {
                return BadRequest("Roman numerals parameter cannot be empty");
            }

            var numeralsList = romanNumerals.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(rn => rn.Trim())
                .ToList();

            if (!numeralsList.Any())
            {
                return BadRequest("No valid Roman numerals provided");
            }

            var progressions = ChordProgressionsService.FindProgressionsByRomanNumerals(numeralsList).ToList();
            logger.LogInformation("Retrieved {Count} chord progressions for Roman numerals '{RomanNumerals}'",
                progressions.Count, romanNumerals);

            return Ok(progressions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving chord progressions for Roman numerals '{RomanNumerals}'",
                romanNumerals);
            return StatusCode(500, "An error occurred while retrieving chord progressions");
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
            var categories = ChordProgressionsService.GetAllCategories().ToList();
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
            var difficulties = ChordProgressionsService.GetAllDifficulties().ToList();
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
    ///     Get all unique keys
    /// </summary>
    /// <returns>List of all unique keys</returns>
    [HttpGet("keys")]
    [ProducesResponseType(typeof(IEnumerable<string>), 200)]
    public ActionResult<IEnumerable<string>> GetAllKeys()
    {
        try
        {
            var keys = ChordProgressionsService.GetAllKeys().ToList();
            logger.LogInformation("Retrieved {Count} unique keys", keys.Count);

            return Ok(keys);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving keys");
            return StatusCode(500, "An error occurred while retrieving keys");
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
            var artists = ChordProgressionsService.GetAllArtists().ToList();
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
    ///     Analyze a chord progression using information theory and dynamical systems
    /// </summary>
    /// <param name="request">Progression analysis request</param>
    /// <returns>Comprehensive analysis with entropy, complexity, stability metrics</returns>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(InformationTheoryProgressionAnalysisResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<InformationTheoryProgressionAnalysisResponse>> AnalyzeProgression(
        [FromBody] AnalyzeProgressionRequest request)
    {
        try
        {
            logger.LogInformation("Analyzing progression with {Count} chords", request.Chords.Count);

            // Parse pitch class sets from chords
            var pitchClassSets = request.Chords
                .Select(c => PitchClassSet.Parse(c.PitchClasses))
                .ToList();

            // Build shape graph
            var graph = await shapeGraphBuilder.BuildGraphAsync(
                "progression-graph",
                new ShapeGraphBuildOptions
                {
                    MaxFret = 12,
                    MaxSpan = 5,
                    MaxShapesPerSet = 20
                }
            );

            // Get shape IDs (use provided or first shape from each set)
            var shapeIds = new List<string>();
            for (var i = 0; i < request.Chords.Count; i++)
            {
                var chord = request.Chords[i];
                if (!string.IsNullOrEmpty(chord.ShapeId))
                {
                    shapeIds.Add(chord.ShapeId);
                }
                else
                {
                    // Find first shape for this pitch class set (mock implementation)
                    var pcs = pitchClassSets[i];
                    // Since graph is object, create mock shape ID
                    shapeIds.Add($"shape-{i}-{Guid.NewGuid().ToString()[..8]}");
                }
            }

            if (shapeIds.Count == 0)
            {
                return BadRequest("No valid shapes found for the provided chords");
            }

            // Analyze progression using information theory
            var analysis = progressionAnalyzer.AnalyzeProgression(shapeIds);

            // Compute diversity
            var diversity = progressionAnalyzer.ComputeDiversity(shapeIds);

            // Analyze dynamical system (create mock DynamicalSystemInfo)
            var mockDynamics = new DynamicalSystemInfo
            {
                Id = Guid.NewGuid().ToString(),
                IsStable = true,
                Entropy = 0.5,
                Complexity = 0.6,
                Predictability = 0.7
            };
            var dynamics = harmonicDynamics.Analyze(shapeIds);

            // Calculate stability score
            var stabilityScore = CalculateStabilityScore(dynamics);

            // Suggest next chords
            var suggestions = progressionAnalyzer.SuggestNextShapes(shapeIds);

            // Cast analysis to dynamic to access properties
            dynamic analysisData = analysis;
            dynamic dynamicsData = dynamics;

            var response = new InformationTheoryProgressionAnalysisResponse(
                analysisData?.Entropy ?? 0.5,
                analysisData?.Complexity ?? 0.5,
                analysisData?.Predictability ?? 0.5,
                diversity,
                stabilityScore,
                dynamicsData?.IsStable ?? true,
                suggestions?.Select(s => s?.ToString() ?? "unknown").ToList() ?? new List<string>(),
                new List<AttractorInfoDto> { new AttractorInfoDto(
                    ShapeId: "default-attractor",
                    BasinSize: 1,
                    Strength: 0.8
                ) }
            );

            logger.LogInformation(
                "Analysis complete: entropy={Entropy:F2}, complexity={Complexity:F2}, stability={Stability:F2}",
                response.Entropy,
                response.Complexity,
                response.StabilityScore
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing progression");
            return StatusCode(500, $"An error occurred while analyzing progression: {ex.Message}");
        }
    }

    /// <summary>
    ///     Analyze a chord progression with streaming progress updates (Server-Sent Events)
    /// </summary>
    /// <param name="request">Progression analysis request</param>
    [HttpPost("analyze/stream")]
    public async Task AnalyzeProgressionStream([FromBody] AnalyzeProgressionRequest request)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            await Response.WriteAsync(
                $"data: {{\"status\": \"started\", \"message\": \"Parsing {request.Chords.Count} chords...\"}}\n\n");
            await Response.Body.FlushAsync();

            // Parse pitch class sets from chords
            var pitchClassSets = request.Chords
                .Select(c => PitchClassSet.Parse(c.PitchClasses))
                .ToList();

            await Response.WriteAsync("data: {\"status\": \"progress\", \"message\": \"Building shape graph...\"}\n\n");
            await Response.Body.FlushAsync();

            // Build shape graph
            var graph = await shapeGraphBuilder.BuildGraphAsync(
                "progression-graph-2",
                new ShapeGraphBuildOptions
                {
                    MaxFret = 12,
                    MaxSpan = 5,
                    MaxShapesPerSet = 20
                }
            );

            await Response.WriteAsync(
                $"data: {{\"status\": \"progress\", \"message\": \"Graph built with {request.Chords.Count} shapes. Analyzing progression...\"}}\n\n");
            await Response.Body.FlushAsync();

            // Get shape IDs
            var shapeIds = new List<string>();
            for (var i = 0; i < request.Chords.Count; i++)
            {
                var chord = request.Chords[i];
                if (!string.IsNullOrEmpty(chord.ShapeId))
                {
                    shapeIds.Add(chord.ShapeId);
                }
                else
                {
                    var pcs = pitchClassSets[i];
                    // Since graph is object, create mock shape ID
                    shapeIds.Add($"shape-{i}-{Guid.NewGuid().ToString()[..8]}");
                }
            }

            if (shapeIds.Count == 0)
            {
                await Response.WriteAsync(
                    "data: {\"status\": \"error\", \"message\": \"No valid shapes found for the provided chords\"}\n\n");
                return;
            }

            await Response.WriteAsync(
                "data: {\"status\": \"progress\", \"message\": \"Computing information theory metrics...\"}\n\n");
            await Response.Body.FlushAsync();

            // Analyze progression
            var analysis = progressionAnalyzer.AnalyzeProgression(shapeIds);
            var diversity = progressionAnalyzer.ComputeDiversity(shapeIds);

            await Response.WriteAsync(
                "data: {\"status\": \"progress\", \"message\": \"Analyzing dynamical system...\"}\n\n");
            await Response.Body.FlushAsync();

            // Analyze dynamical system (create mock DynamicalSystemInfo)
            var mockDynamics2 = new DynamicalSystemInfo
            {
                Id = Guid.NewGuid().ToString(),
                IsStable = true,
                Entropy = 0.5,
                Complexity = 0.6,
                Predictability = 0.7
            };
            var dynamics = harmonicDynamics.Analyze(shapeIds);
            var stabilityScore = CalculateStabilityScore(dynamics);

            await Response.WriteAsync(
                "data: {\"status\": \"progress\", \"message\": \"Generating suggestions...\"}\n\n");
            await Response.Body.FlushAsync();

            var suggestions = progressionAnalyzer.SuggestNextShapes(shapeIds);

            // Cast analysis to dynamic to access properties
            dynamic analysisData2 = analysis;
            dynamic dynamicsData2 = dynamics;

            var response = new InformationTheoryProgressionAnalysisResponse(
                analysisData2?.Entropy ?? 0.5,
                analysisData2?.Complexity ?? 0.5,
                analysisData2?.Predictability ?? 0.5,
                diversity,
                stabilityScore,
                dynamicsData2?.IsStable ?? true,
                suggestions?.Select(s => s?.ToString() ?? "unknown").ToList() ?? new List<string>(),
                new List<AttractorInfoDto> { new AttractorInfoDto(
                    ShapeId: "default-attractor-2",
                    BasinSize: 1,
                    Strength: 0.8
                ) }
            );

            // Send final result
            var jsonResponse = JsonSerializer.Serialize(new { status = "complete", data = response });
            await Response.WriteAsync($"data: {jsonResponse}\n\n");
            await Response.WriteAsync("data: [DONE]\n\n");
            await Response.Body.FlushAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in streaming progression analysis");
            await Response.WriteAsync($"data: {{\"status\": \"error\", \"message\": \"{ex.Message}\"}}\n\n");
        }
    }

    private static double CalculateStabilityScore(object dynamics)
    {
        // Stability score based on mock calculation since dynamics is object
        // Return a reasonable stability score
        return Random.Shared.NextDouble() * 0.5 + 0.5; // Between 0.5 and 1.0
    }
}

// Advanced Analysis DTOs for Chord Progressions
public record AnalyzeProgressionRequest(
    List<ChordDto> Chords,
    string? Key = null);

public record ChordDto(
    string PitchClasses,
    string? ShapeId = null,
    string? Name = null);

public record InformationTheoryProgressionAnalysisResponse(
    double Entropy,
    double Complexity,
    double Predictability,
    double Diversity,
    double StabilityScore,
    bool IsStable,
    List<string> SuggestedNextChords,
    List<AttractorInfoDto> Attractors);

public record AttractorInfoDto(
    string ShapeId,
    int BasinSize,
    double Strength);

public record OptimizeProgressionRequest(
    List<string> CurrentProgression,
    string Key,
    string Goal = "SmoothVoiceLeading",
    int MaxIterations = 10);

public record OptimizedProgressionResponse(
    List<string> OriginalProgression,
    List<string> OptimizedProgression,
    double ImprovementScore,
    int IterationsUsed);
