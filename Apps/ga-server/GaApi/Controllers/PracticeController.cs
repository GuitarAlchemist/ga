namespace GaApi.Controllers;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GaApi.Services;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// REST API controller for practice routines, ear training, and fretboard exploration.
/// Provides endpoints for generating personalized practice sessions and quizzes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PracticeController(
    PracticeRoutineService practiceService,
    EarTrainingService earTrainingService,
    FretboardContextService fretboardService,
    ILogger<PracticeController> logger)
    : ControllerBase
{
    /// <summary>
    /// Generate a practice routine for a specific technique, difficulty, and duration.
    /// </summary>
    [HttpPost("routines/generate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> GenerateRoutineAsync(
        [FromBody] GeneratePracticeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (string.IsNullOrWhiteSpace(request.FocusArea))
            return BadRequest(new { error = "FocusArea is required." });

        if (request.DurationMinutes < 5 || request.DurationMinutes > 120)
            return BadRequest(new { error = "Duration must be between 5 and 120 minutes." });

        // Parse enum safely
        if (!System.Enum.TryParse<PracticeRoutineService.FocusArea>(request.FocusArea, out var focusArea))
            return BadRequest(new { error = $"Invalid FocusArea: {request.FocusArea}" });

        if (!System.Enum.TryParse<PracticeRoutineService.DifficultyLevel>(request.Difficulty ?? "Intermediate", out var difficulty))
            return BadRequest(new { error = $"Invalid Difficulty: {request.Difficulty}" });

        try
        {
            var result = await practiceService.GenerateRoutineAsync(
                focusArea,
                difficulty,
                request.DurationMinutes,
                request.UserBackground);

            return result switch
            {
                Result<PracticeRoutineService.PracticeRoutine>.Ok ok =>
                    Ok(new
                    {
                        success = true,
                        routine = ok.Value
                    }),
                Result<PracticeRoutineService.PracticeRoutine>.Fail fail =>
                    StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        success = false,
                        error = fail.Message
                    }),
                _ => StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "Unknown result type." })
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating practice routine");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "Failed to generate practice routine."
            });
        }
    }

    /// <summary>
    /// Get a pre-built practice routine template.
    /// </summary>
    [HttpGet("routines/template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<object> GetTemplateRoutine(
        [FromQuery] string focusArea = "Scales",
        [FromQuery] string difficulty = "Beginner")
    {
        if (!System.Enum.TryParse<PracticeRoutineService.FocusArea>(focusArea, out var focus))
            return BadRequest(new { error = $"Invalid FocusArea: {focusArea}" });

        if (!System.Enum.TryParse<PracticeRoutineService.DifficultyLevel>(difficulty, out var diff))
            return BadRequest(new { error = $"Invalid Difficulty: {difficulty}" });

        var routine = practiceService.GetTemplateRoutine(focus, diff);
        return Ok(new { success = true, routine });
    }

    /// <summary>
    /// Generate an interval recognition quiz.
    /// </summary>
    [HttpPost("ear-training/intervals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> GenerateIntervalQuizAsync(
        [FromBody] GenerateEarTrainingRequest request)
    {
        if (request.QuestionCount < 5 || request.QuestionCount > 50)
            return BadRequest(new { error = "QuestionCount must be between 5 and 50." });

        if (!System.Enum.TryParse<EarTrainingService.IntervalDifficulty>(request.Difficulty ?? "Basic", out var difficulty))
            return BadRequest(new { error = $"Invalid Difficulty: {request.Difficulty}" });

        try
        {
            var result = await earTrainingService.GenerateIntervalQuizAsync(difficulty, request.QuestionCount);

            return result switch
            {
                Result<EarTrainingService.EarTrainingQuiz>.Ok ok =>
                    Ok(new { success = true, quiz = ok.Value }),
                Result<EarTrainingService.EarTrainingQuiz>.Fail fail =>
                    StatusCode(StatusCodes.Status500InternalServerError,
                        new { success = false, error = fail.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "Unknown result type." })
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating interval quiz");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to generate interval quiz." });
        }
    }

    /// <summary>
    /// Generate a chord quality recognition quiz.
    /// </summary>
    [HttpPost("ear-training/chords")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> GenerateChordQuizAsync(
        [FromBody] GenerateEarTrainingRequest request)
    {
        if (request.QuestionCount < 5 || request.QuestionCount > 50)
            return BadRequest(new { error = "QuestionCount must be between 5 and 50." });

        try
        {
            var result = await earTrainingService.GenerateChordQuizAsync(request.QuestionCount);

            return result switch
            {
                Result<EarTrainingService.EarTrainingQuiz>.Ok ok =>
                    Ok(new { success = true, quiz = ok.Value }),
                Result<EarTrainingService.EarTrainingQuiz>.Fail fail =>
                    StatusCode(StatusCodes.Status500InternalServerError,
                        new { success = false, error = fail.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "Unknown result type." })
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating chord quiz");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to generate chord quiz." });
        }
    }

    /// <summary>
    /// Generate a scale/mode recognition quiz.
    /// </summary>
    [HttpPost("ear-training/scales")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> GenerateScaleQuizAsync(
        [FromBody] GenerateEarTrainingRequest request)
    {
        if (request.QuestionCount < 5 || request.QuestionCount > 50)
            return BadRequest(new { error = "QuestionCount must be between 5 and 50." });

        try
        {
            var result = await earTrainingService.GenerateScaleQuizAsync(request.QuestionCount);

            return result switch
            {
                Result<EarTrainingService.EarTrainingQuiz>.Ok ok =>
                    Ok(new { success = true, quiz = ok.Value }),
                Result<EarTrainingService.EarTrainingQuiz>.Fail fail =>
                    StatusCode(StatusCodes.Status500InternalServerError,
                        new { success = false, error = fail.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "Unknown result type." })
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating scale quiz");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to generate scale quiz." });
        }
    }

    /// <summary>
    /// Get a progressive ear training curriculum.
    /// </summary>
    [HttpGet("ear-training/curriculum")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> GetCurriculum()
    {
        var curriculum = earTrainingService.GetProgressiveCurriculum();
        return Ok(new { success = true, curriculum });
    }

    /// <summary>
    /// Get scale shapes across all CAGED positions.
    /// </summary>
    [HttpGet("fretboard/scale-shapes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<object> GetScaleShapes(
        [FromQuery] string scaleName = "Major",
        [FromQuery] string rootNote = "C")
    {
        if (string.IsNullOrWhiteSpace(scaleName) || string.IsNullOrWhiteSpace(rootNote))
            return BadRequest(new { error = "ScaleName and RootNote are required." });

        try
        {
            var shapes = fretboardService.GetScaleShapes(scaleName, rootNote);
            return Ok(new { success = true, scaleShapes = shapes });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting scale shapes");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to get scale shapes." });
        }
    }

    /// <summary>
    /// Get mode exploration with fretboard context.
    /// </summary>
    [HttpGet("fretboard/modes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<object> GetModeExploration(
        [FromQuery] string parentScale = "Major",
        [FromQuery] string rootNote = "C")
    {
        if (string.IsNullOrWhiteSpace(parentScale) || string.IsNullOrWhiteSpace(rootNote))
            return BadRequest(new { error = "ParentScale and RootNote are required." });

        try
        {
            var exploration = fretboardService.GetModeExploration(parentScale, rootNote);
            return Ok(new { success = true, exploration });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting mode exploration");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to get mode exploration." });
        }
    }

    /// <summary>
    /// Get arpeggio shapes for a chord.
    /// </summary>
    [HttpGet("fretboard/arpeggio-shapes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<object> GetArpeggioShapes(
        [FromQuery] string chordName = "Major",
        [FromQuery] string rootNote = "C")
    {
        if (string.IsNullOrWhiteSpace(chordName) || string.IsNullOrWhiteSpace(rootNote))
            return BadRequest(new { error = "ChordName and RootNote are required." });

        try
        {
            var shapes = fretboardService.GetArpeggioShapes(chordName, rootNote);
            return Ok(new { success = true, arpeggioShapes = shapes });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting arpeggio shapes");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to get arpeggio shapes." });
        }
    }

    /// <summary>
    /// Get a beginner-friendly scale exploration guide.
    /// </summary>
    [HttpGet("fretboard/scale-guide")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<object> GetScaleGuide(
        [FromQuery] string scaleName = "Major",
        [FromQuery] string rootNote = "C")
    {
        if (string.IsNullOrWhiteSpace(scaleName) || string.IsNullOrWhiteSpace(rootNote))
            return BadRequest(new { error = "ScaleName and RootNote are required." });

        try
        {
            var guide = fretboardService.GetBeginnerScaleGuide(scaleName, rootNote);
            return Ok(new { success = true, guide });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting scale guide");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to get scale guide." });
        }
    }

    /// <summary>
    /// Generate a full fretboard map for a scale.
    /// </summary>
    [HttpGet("fretboard/map")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<object> GenerateFretboardMap(
        [FromQuery] string scaleName = "Major",
        [FromQuery] string rootNote = "C")
    {
        if (string.IsNullOrWhiteSpace(scaleName) || string.IsNullOrWhiteSpace(rootNote))
            return BadRequest(new { error = "ScaleName and RootNote are required." });

        try
        {
            var map = fretboardService.GenerateFretboardMap(scaleName, rootNote);
            return Ok(new { success = true, map });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating fretboard map");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "Failed to generate fretboard map." });
        }
    }

    /// <summary>
    /// Get available practice focus areas.
    /// </summary>
    [HttpGet("focus-areas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> GetFocusAreas()
    {
        var focusAreas = System.Enum.GetValues<PracticeRoutineService.FocusArea>()
            .Select(f => new { name = f.ToString(), value = f })
            .ToList();

        return Ok(new { success = true, focusAreas });
    }

    /// <summary>
    /// Get available difficulty levels.
    /// </summary>
    [HttpGet("difficulty-levels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> GetDifficultyLevels()
    {
        var levels = System.Enum.GetValues<PracticeRoutineService.DifficultyLevel>()
            .Select(l => new { name = l.ToString(), value = l })
            .ToList();

        return Ok(new { success = true, levels });
    }
}

/// <summary>
/// Request model for generating practice routines.
/// </summary>
public record GeneratePracticeRequest(
    string FocusArea,
    string? Difficulty = "Intermediate",
    int DurationMinutes = 15,
    string? UserBackground = null);

/// <summary>
/// Request model for generating ear training quizzes.
/// </summary>
public record GenerateEarTrainingRequest(
    int QuestionCount = 10,
    string? Difficulty = null);
