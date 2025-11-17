namespace GA.Analytics.Service.Controllers;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

using System.ComponentModel.DataAnnotations;
using GA.Business.Core.Invariants;
using GA.Analytics.Service.Models;
using GA.Analytics.Service.Services;

/// <summary>
///     Controller for invariant validation and monitoring
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InvariantsController(
    ILogger<InvariantsController> logger,
    InvariantValidationService validationService,
    RealtimeInvariantMonitoringService monitoringService)
    : ControllerBase
{
    /// <summary>
    ///     Validate a specific musical concept by name and type
    /// </summary>
    /// <param name="conceptName">Name of the musical concept</param>
    /// <param name="conceptType">Type of concept (IconicChord, ChordProgression, GuitarTechnique, SpecializedTuning)</param>
    /// <returns>Validation result with any invariant violations</returns>
    [HttpGet("validate-concept")]
    [ProducesResponseType(typeof(CompositeInvariantValidationResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public ActionResult<CompositeInvariantValidationResult> ValidateConcept(
        [FromQuery] [Required] string conceptName,
        [FromQuery] [Required] string conceptType)
    {
        try
        {
            logger.LogInformation("Validating concept: {ConceptName} of type {ConceptType}", conceptName, conceptType);

            var result = await validationService.ValidateConcept(conceptName,
                new Dictionary<string, object> { ["conceptType"] = conceptType });

            logger.LogInformation("Validation completed for {ConceptName}", conceptName);

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning("Invalid concept validation request: {Message}", ex.Message);
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating concept {ConceptName} of type {ConceptType}", conceptName,
                conceptType);
            return StatusCode(500, "An error occurred while validating the concept");
        }
    }

    /// <summary>
    ///     Validate all musical concepts in the knowledge base
    /// </summary>
    /// <returns>Global validation result with statistics and detailed results</returns>
    [HttpPost("validate-all")]
    [ProducesResponseType(typeof(GlobalValidationResult), 200)]
    public async Task<ActionResult<GlobalValidationResult>> ValidateAll()
    {
        try
        {
            logger.LogInformation("Starting global validation of all musical concepts");

            var result = await validationService.ValidateAllAsync();

            logger.LogInformation("Global validation completed");

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during global validation");
            return StatusCode(500, "An error occurred during global validation");
        }
    }

    /// <summary>
    ///     Get validation statistics for the entire knowledge base
    /// </summary>
    /// <returns>Comprehensive validation statistics</returns>
    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ValidationStatistics), 200)]
    public async Task<ActionResult<ValidationStatistics>> GetValidationStatistics()
    {
        try
        {
            logger.LogDebug("Generating validation statistics");

            var statistics = await validationService.GetValidationStatisticsAsync();

            logger.LogDebug("Generated validation statistics: {TotalConcepts} concepts, {TotalViolations} violations",
                statistics.TotalConcepts, statistics.TotalViolations);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating validation statistics");
            return StatusCode(500, "An error occurred while generating validation statistics");
        }
    }

    /// <summary>
    ///     Get real-time violation statistics and health metrics
    /// </summary>
    /// <returns>Current violation statistics and system health score</returns>
    [HttpGet("violation-statistics")]
    [ProducesResponseType(typeof(ViolationStatistics), 200)]
    public async Task<ActionResult<ViolationStatistics>> GetViolationStatistics()
    {
        try
        {
            logger.LogDebug("Getting real-time violation statistics");

            var statistics = await monitoringService.GetViolationStatisticsAsync();

            logger.LogDebug(
                "Retrieved violation statistics: {CriticalViolations} critical, {HealthScore:P} health score",
                statistics.CriticalViolations, statistics.OverallHealthScore);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting violation statistics");
            return StatusCode(500, "An error occurred while getting violation statistics");
        }
    }

    /// <summary>
    ///     Get recent invariant violation events
    /// </summary>
    /// <param name="maxCount">Maximum number of recent violations to return (1-100)</param>
    /// <returns>List of recent violation events</returns>
    [HttpGet("recent-violations")]
    [ProducesResponseType(typeof(List<InvariantViolationEvent>), 200)]
    [ProducesResponseType(400)]
    public ActionResult<List<InvariantViolationEvent>> GetRecentViolations([FromQuery] int maxCount = 50)
    {
        try
        {
            if (maxCount is < 1 or > 100)
            {
                return BadRequest("maxCount must be between 1 and 100");
            }

            logger.LogDebug("Getting {MaxCount} recent violation events", maxCount);

            var violations = monitoringService.GetRecentViolations(maxCount);

            logger.LogDebug("Retrieved {ViolationCount} recent violation events", violations.Count);

            return Ok(violations);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting recent violations");
            return StatusCode(500, "An error occurred while getting recent violations");
        }
    }

    /// <summary>
    ///     Validate a specific concept and monitor for violations
    /// </summary>
    /// <param name="conceptName">Name of the musical concept</param>
    /// <param name="conceptType">Type of concept</param>
    /// <returns>List of invariant violations found</returns>
    [HttpPost("monitor-concept")]
    [ProducesResponseType(typeof(List<InvariantValidationResult>), 200)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<List<InvariantValidationResult>>> MonitorConcept(
        [FromQuery] [Required] string conceptName,
        [FromQuery] [Required] string conceptType)
    {
        try
        {
            logger.LogInformation("Monitoring concept: {ConceptName} of type {ConceptType}", conceptName, conceptType);

            var violations = await monitoringService.ValidateConceptAsync(conceptName, conceptType);

            logger.LogInformation("Monitoring completed for {ConceptName}: {ViolationCount} violations found",
                conceptName, violations.Count);

            return Ok(violations);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error monitoring concept {ConceptName} of type {ConceptType}", conceptName,
                conceptType);
            return StatusCode(500, "An error occurred while monitoring the concept");
        }
    }

    /// <summary>
    ///     Clear the violation event queue
    /// </summary>
    /// <returns>Confirmation of queue clearing</returns>
    [HttpPost("clear-violation-queue")]
    [ProducesResponseType(typeof(object), 200)]
    public ActionResult ClearViolationQueue()
    {
        try
        {
            logger.LogInformation("Clearing violation event queue");

            monitoringService.ClearViolationQueue();

            logger.LogInformation("Violation event queue cleared successfully");

            return Ok(new
            {
                message = "Violation queue cleared successfully",
                clearedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing violation queue");
            return StatusCode(500, "An error occurred while clearing the violation queue");
        }
    }

    /// <summary>
    ///     Get validation summary for all concept types
    /// </summary>
    /// <returns>Summary of validation results by concept type</returns>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> GetValidationSummary()
    {
        try
        {
            logger.LogDebug("Generating validation summary");

            var globalResult = await validationService.ValidateAllAsync();
            var summary = globalResult.GetSummary();

            var conceptSummaries = new
            {
                IconicChords = new
                {
                    TotalConcepts = globalResult.IconicChordResults.Count,
                    ValidConcepts = globalResult.IconicChordResults.Values.Count(r => r.IsValid),
                    TotalViolations = globalResult.IconicChordResults.Values.Sum(r => r.Failures.Count()),
                    CriticalViolations = globalResult.IconicChordResults.Values.Sum(r =>
                        r.GetFailuresBySeverity(InvariantSeverity.Critical).Count())
                },
                ChordProgressions = new
                {
                    TotalConcepts = globalResult.ChordProgressionResults.Count,
                    ValidConcepts = globalResult.ChordProgressionResults.Values.Count(r => r.IsValid),
                    TotalViolations = globalResult.ChordProgressionResults.Values.Sum(r => r.Failures.Count()),
                    CriticalViolations = globalResult.ChordProgressionResults.Values.Sum(r =>
                        r.GetFailuresBySeverity(InvariantSeverity.Critical).Count())
                },
                GuitarTechniques = new
                {
                    TotalConcepts = globalResult.GuitarTechniqueResults.Count,
                    ValidConcepts = globalResult.GuitarTechniqueResults.Values.Count(r => r.IsValid),
                    TotalViolations = globalResult.GuitarTechniqueResults.Values.Sum(r => r.Failures.Count()),
                    CriticalViolations = globalResult.GuitarTechniqueResults.Values.Sum(r =>
                        r.GetFailuresBySeverity(InvariantSeverity.Critical).Count())
                },
                SpecializedTunings = new
                {
                    TotalConcepts = globalResult.SpecializedTuningResults.Count,
                    ValidConcepts = globalResult.SpecializedTuningResults.Values.Count(r => r.IsValid),
                    TotalViolations = globalResult.SpecializedTuningResults.Values.Sum(r => r.Failures.Count()),
                    CriticalViolations = globalResult.SpecializedTuningResults.Values.Sum(r =>
                        r.GetFailuresBySeverity(InvariantSeverity.Critical).Count())
                }
            };

            var result = new
            {
                OverallSummary = new
                {
                    summary.TotalConcepts,
                    summary.ValidConcepts,
                    summary.TotalInvariants,
                    summary.TotalViolations,
                    summary.CriticalViolations,
                    summary.ErrorViolations,
                    summary.WarningViolations,
                    summary.InfoViolations,
                    summary.OverallSuccessRate,
                    summary.Duration,
                    summary.GeneratedAt
                },
                ConceptSummaries = conceptSummaries
            };

            logger.LogDebug("Generated validation summary: {TotalConcepts} concepts, {SuccessRate:P} success rate",
                summary.TotalConcepts, summary.OverallSuccessRate);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating validation summary");
            return StatusCode(500, "An error occurred while generating validation summary");
        }
    }

    /// <summary>
    ///     Get health status of the invariants system
    /// </summary>
    /// <returns>Health status and key metrics</returns>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<ActionResult> GetHealthStatus()
    {
        try
        {
            var violationStats = await monitoringService.GetViolationStatisticsAsync();
            var validationStats = await validationService.GetValidationStatisticsAsync();

            var healthStatus = new
            {
                Status = violationStats.CriticalViolations == 0 ? "Healthy" : "Critical",
                violationStats.OverallHealthScore,
                TotalViolations = violationStats.CriticalViolations + violationStats.ErrorViolations + violationStats.WarningViolations,
                violationStats.CriticalViolations,
                violationStats.ErrorViolations,
                violationStats.WarningViolations,
                validationStats.TotalConcepts,
                validationStats.OverallSuccessRate,
                LastChecked = DateTime.UtcNow
            };

            return Ok(healthStatus);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting health status");
            return StatusCode(500, new
            {
                Status = "Error",
                Message = "Unable to determine health status",
                LastChecked = DateTime.UtcNow
            });
        }
    }
}
