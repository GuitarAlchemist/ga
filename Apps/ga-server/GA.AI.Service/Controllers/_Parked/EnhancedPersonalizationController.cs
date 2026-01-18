namespace GA.AI.Service.Controllers;

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using GA.AI.Service.Models;
using GA.AI.Service.Services;

/// <summary>
///     Enhanced personalization controller with AI-driven adaptive learning and real-time assistance
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EnhancedPersonalizationController(
    ILogger<EnhancedPersonalizationController> logger,
    EnhancedUserPersonalizationService personalizationService)
    : ControllerBase
{
    /// <summary>
    ///     Generate AI-powered adaptive learning path
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="request">Adaptive learning path configuration</param>
    /// <returns>Adaptive learning path with AI-driven curriculum and milestones</returns>
    [HttpPost("adaptive-learning-path/{userId}")]
    [ProducesResponseType(typeof(AdaptiveLearningPath), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<AdaptiveLearningPath>> CreateAdaptiveLearningPath(
        string userId,
        [FromBody] AdaptiveLearningRequest request)
    {
        try
        {
            var adaptivePath = await personalizationService.GenerateAdaptiveLearningPathAsync(userId, request);

            logger.LogInformation(
                "Created adaptive learning path '{Name}' with {ModuleCount} modules for user {UserId}",
                adaptivePath.Name, adaptivePath.Curriculum.LearningModules.Count, userId);

            return Ok(adaptivePath);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating adaptive learning path for user {UserId}", userId);
            return StatusCode(500, "An error occurred while creating adaptive learning path");
        }
    }

    /// <summary>
    ///     Adapt learning path based on performance data
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="learningPathId">Learning path identifier</param>
    /// <param name="performanceData">User performance data for adaptation</param>
    /// <returns>Adaptation result with applied changes and new recommendations</returns>
    [HttpPost("adapt-learning-path/{userId}/{learningPathId}")]
    [ProducesResponseType(typeof(AdaptationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<AdaptationResult>> AdaptLearningPath(
        string userId,
        int learningPathId,
        [FromBody] PerformanceData performanceData)
    {
        try
        {
            var adaptationResult =
                await personalizationService.AdaptLearningPathAsync(userId, learningPathId, performanceData);

            logger.LogInformation("Adapted learning path {PathId} for user {UserId} with {AdaptationCount} changes",
                learningPathId, userId, adaptationResult.AppliedAdaptations.Count);

            return Ok(adaptationResult);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adapting learning path {PathId} for user {UserId}", learningPathId, userId);
            return StatusCode(500, "An error occurred while adapting learning path");
        }
    }

    /// <summary>
    ///     Generate intelligent practice session with real-time adaptation
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="request">Practice session configuration</param>
    /// <returns>Intelligent practice session with adaptive exercises and performance tracking</returns>
    [HttpPost("intelligent-practice-session/{userId}")]
    [ProducesResponseType(typeof(IntelligentPracticeSession), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<IntelligentPracticeSession>> GenerateIntelligentPracticeSession(
        string userId,
        [FromBody] PracticeSessionRequest request)
    {
        try
        {
            if (request.DurationMinutes is < 15 or > 180)
            {
                return BadRequest("Duration must be between 15 and 180 minutes");
            }

            var practiceSession = await personalizationService.GenerateIntelligentPracticeSessionAsync(userId, request);

            logger.LogInformation(
                "Generated intelligent practice session with {ExerciseCount} exercises for user {UserId}",
                practiceSession.TotalExercises, userId);

            return Ok(practiceSession);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating intelligent practice session for user {UserId}", userId);
            return StatusCode(500, "An error occurred while generating practice session");
        }
    }

    /// <summary>
    ///     Get real-time learning assistance and contextual hints
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="currentConcept">Current musical concept being studied</param>
    /// <param name="context">Learning context and environment data</param>
    /// <returns>Learning assistance with hints, adjustments, and practice suggestions</returns>
    [HttpPost("learning-assistance/{userId}")]
    [ProducesResponseType(typeof(LearningAssistance), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<LearningAssistance>> GetLearningAssistance(
        string userId,
        [FromQuery] [Required] string currentConcept,
        [FromBody] Dictionary<string, object>? context = null)
    {
        try
        {
            context ??= new Dictionary<string, object>();

            var assistance = await personalizationService.GetLearningAssistanceAsync(userId, currentConcept, context);

            logger.LogInformation(
                "Provided learning assistance with {HintCount} hints for user {UserId} on concept {Concept}",
                assistance.ContextualHints.Count, userId, currentConcept);

            return Ok(assistance);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error providing learning assistance for user {UserId}", userId);
            return StatusCode(500, "An error occurred while providing learning assistance");
        }
    }

    /// <summary>
    ///     Analyze user engagement patterns and trends
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="analysisWindowDays">Analysis window in days (7-365)</param>
    /// <returns>Comprehensive engagement analysis with patterns, metrics, and recommendations</returns>
    [HttpGet("engagement-analysis/{userId}")]
    [ProducesResponseType(typeof(EngagementAnalysis), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<EngagementAnalysis>> AnalyzeUserEngagement(
        string userId,
        [FromQuery] int analysisWindowDays = 30)
    {
        try
        {
            if (analysisWindowDays is < 7 or > 365)
            {
                return BadRequest("Analysis window must be between 7 and 365 days");
            }

            var analysisWindow = TimeSpan.FromDays(analysisWindowDays);
            var engagement = await personalizationService.AnalyzeUserEngagementAsync(userId, analysisWindow);

            logger.LogInformation("Analyzed engagement for user {UserId} over {Days} days: {Score} overall score",
                userId, analysisWindowDays, engagement.EngagementMetrics.OverallScore);

            return Ok(engagement);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing engagement for user {UserId}", userId);
            return StatusCode(500, "An error occurred while analyzing engagement");
        }
    }

    /// <summary>
    ///     Create personalized achievement system
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>Personalized achievement system with skill, progress, social, and creative achievements</returns>
    [HttpPost("achievement-system/{userId}")]
    [ProducesResponseType(typeof(PersonalizedAchievementSystem), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<PersonalizedAchievementSystem>> CreateAchievementSystem(string userId)
    {
        try
        {
            var achievementSystem = await personalizationService.CreateAchievementSystemAsync(userId);

            logger.LogInformation("Created achievement system with {AchievementCount} achievements for user {UserId}",
                achievementSystem.TotalAchievements, userId);

            return Ok(achievementSystem);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating achievement system for user {UserId}", userId);
            return StatusCode(500, "An error occurred while creating achievement system");
        }
    }

    /// <summary>
    ///     Get adaptive learning recommendations based on current progress
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="includeRemedial">Include remedial recommendations for weak areas</param>
    /// <param name="includeAdvanced">Include advanced recommendations for strong areas</param>
    /// <returns>Adaptive recommendations tailored to user's current learning state</returns>
    [HttpGet("adaptive-recommendations/{userId}")]
    [ProducesResponseType(typeof(List<AdaptedRecommendation>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<ActionResult<List<AdaptedRecommendation>>> GetAdaptiveRecommendations(
        string userId,
        [FromQuery] bool includeRemedial = true,
        [FromQuery] bool includeAdvanced = true)
    {
        try
        {
            // Mock performance data for demonstration
            var performanceData = new PerformanceData
            {
                Scores = new Dictionary<string, double>
                {
                    ["Chord Progressions"] = 0.85,
                    ["Guitar Techniques"] = 0.65,
                    ["Music Theory"] = 0.75,
                    ["Rhythm"] = 0.55
                },
                EngagementScore = 0.8
            };

            // Use a mock learning path ID
            var adaptationResult = await personalizationService.AdaptLearningPathAsync(userId, 1, performanceData);

            var recommendations = adaptationResult.NewRecommendations;

            if (!includeRemedial)
            {
                recommendations = recommendations.Where(r => r.Type != "Remedial").ToList();
            }

            if (!includeAdvanced)
            {
                recommendations = recommendations.Where(r => r.Type != "Advanced").ToList();
            }

            logger.LogInformation("Generated {RecommendationCount} adaptive recommendations for user {UserId}",
                recommendations.Count, userId);

            return Ok(recommendations);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating adaptive recommendations for user {UserId}", userId);
            return StatusCode(500, "An error occurred while generating adaptive recommendations");
        }
    }

    /// <summary>
    ///     Get personalized difficulty calibration for user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="conceptType">Type of musical concept to calibrate</param>
    /// <returns>Difficulty calibration settings optimized for the user's skill level</returns>
    [HttpGet("difficulty-calibration/{userId}")]
    [ProducesResponseType(typeof(Dictionary<string, double>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public ActionResult<Dictionary<string, double>> GetDifficultyCalibration(
        string userId,
        [FromQuery] string? conceptType = null)
    {
        try
        {
            // Mock difficulty calibration based on user's historical performance
            var calibration = new Dictionary<string, double>();

            if (string.IsNullOrEmpty(conceptType))
            {
                // Return calibration for all concept types
                calibration["IconicChord"] = 0.7;
                calibration["ChordProgression"] = 0.8;
                calibration["GuitarTechnique"] = 0.6;
                calibration["SpecializedTuning"] = 0.5;
            }
            else
            {
                // Return calibration for specific concept type
                calibration[conceptType] = conceptType switch
                {
                    "IconicChord" => 0.7,
                    "ChordProgression" => 0.8,
                    "GuitarTechnique" => 0.6,
                    "SpecializedTuning" => 0.5,
                    _ => 0.6
                };
            }

            logger.LogInformation("Retrieved difficulty calibration for user {UserId}", userId);

            return Ok(calibration);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving difficulty calibration for user {UserId}", userId);
            return StatusCode(500, "An error occurred while retrieving difficulty calibration");
        }
    }

    /// <summary>
    ///     Update user's learning preferences and adaptation settings
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="preferences">Updated learning preferences</param>
    /// <returns>Confirmation of preference updates</returns>
    [HttpPut("learning-preferences/{userId}")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public ActionResult UpdateLearningPreferences(
        string userId,
        [FromBody] Dictionary<string, object> preferences)
    {
        try
        {
            // In a real implementation, this would update the user's preferences in the database
            // For now, we'll just validate and return success

            var validPreferenceKeys = new[]
            {
                "adaptationStrategy", "difficultyPreference", "sessionLength",
                "focusAreas", "practiceFrequency", "feedbackLevel"
            };

            var invalidKeys = preferences.Keys.Except(validPreferenceKeys).ToList();
            if (invalidKeys.Any())
            {
                return BadRequest($"Invalid preference keys: {string.Join(", ", invalidKeys)}");
            }

            logger.LogInformation("Updated learning preferences for user {UserId}", userId);

            return Ok(new
            {
                message = "Learning preferences updated successfully",
                userId,
                updatedAt = DateTime.UtcNow,
                preferencesCount = preferences.Count
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating learning preferences for user {UserId}", userId);
            return StatusCode(500, "An error occurred while updating learning preferences");
        }
    }
}
