using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using GA.Business.Core.Configuration;
using GA.Business.Core.Invariants;
using System.Threading.Channels;

namespace GA.Business.Core.Services;

/// <summary>
/// Real-time monitoring service for invariant violations during configuration updates
/// OPTIMIZED: Uses System.Threading.Channels for lock-free, backpressure-aware processing
/// </summary>
public class RealtimeInvariantMonitoringService(
    ILogger<RealtimeInvariantMonitoringService> logger,
    IServiceProvider serviceProvider,
    ConfigurationBroadcastService? broadcastService = null)
    : BackgroundService
{
    // OPTIMIZATION: Channel with bounded capacity provides automatic backpressure
    // DropOldest ensures we don't lose recent violations under high load
    private readonly Channel<InvariantViolationEvent> _violationChannel = Channel.CreateBounded<InvariantViolationEvent>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting real-time invariant monitoring service (Channel-based)");

        try
        {
            // OPTIMIZATION: Process violations as they arrive (no polling delay!)
            await foreach (var violation in _violationChannel.Reader.ReadAllAsync(stoppingToken))
            {
                await ProcessViolationAsync(violation);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Real-time invariant monitoring service was cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in real-time invariant monitoring service");
        }
    }

    /// <summary>
    /// Monitor configuration changes and validate invariants
    /// </summary>
    public async Task MonitorConfigurationChangeAsync(string configurationType, string fileName)
    {
        logger.LogInformation("Monitoring configuration change: {ConfigurationType} - {FileName}", configurationType, fileName);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var validationService = scope.ServiceProvider.GetService<InvariantValidationService>();
            
            if (validationService == null)
            {
                logger.LogWarning("InvariantValidationService not available for monitoring");
                return;
            }

            // Validate the updated configuration
            var violations = await ValidateConfigurationTypeAsync(validationService, configurationType);
            
            if (violations.Any())
            {
                logger.LogWarning("Found {ViolationCount} invariant violations after configuration update", violations.Count);

                // Queue violations for processing (lock-free!)
                foreach (var violation in violations)
                {
                    await _violationChannel.Writer.WriteAsync(new InvariantViolationEvent
                    {
                        ConfigurationType = configurationType,
                        FileName = fileName,
                        Violation = violation,
                        DetectedAt = DateTime.UtcNow
                    });
                }

                // Broadcast critical violations immediately
                var criticalViolations = violations.Where(v => v.Severity == InvariantSeverity.Critical).ToList();
                if (criticalViolations.Any() && broadcastService != null)
                {
                    await broadcastService.BroadcastConfigurationError(configurationType, 
                        $"Critical invariant violations detected: {criticalViolations.Count} violations");
                }
            }
            else
            {
                logger.LogInformation("No invariant violations found after configuration update");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error monitoring configuration change for {ConfigurationType}", configurationType);
        }
    }

    /// <summary>
    /// Validate a specific concept and report violations
    /// </summary>
    public async Task<List<InvariantValidationResult>> ValidateConceptAsync(string conceptName, string conceptType)
    {
        logger.LogDebug("Validating concept: {ConceptName} of type {ConceptType}", conceptName, conceptType);

        try
        {
            using var scope = serviceProvider.CreateScope();
            var validationService = scope.ServiceProvider.GetService<InvariantValidationService>();
            
            if (validationService == null)
            {
                logger.LogWarning("InvariantValidationService not available");
                return [];
            }

            var result = validationService.ValidateConcept(conceptName, conceptType);
            var violations = result.Failures.ToList();

            if (violations.Any())
            {
                logger.LogWarning("Concept {ConceptName} has {ViolationCount} invariant violations",
                                 conceptName, violations.Count);

                // Queue violations for processing (lock-free!)
                foreach (var violation in violations)
                {
                    await _violationChannel.Writer.WriteAsync(new InvariantViolationEvent
                    {
                        ConceptName = conceptName,
                        ConceptType = conceptType,
                        Violation = violation,
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }

            return violations;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating concept {ConceptName} of type {ConceptType}", conceptName, conceptType);
            return [];
        }
    }

    /// <summary>
    /// Get current violation statistics
    /// </summary>
    public async Task<ViolationStatistics> GetViolationStatisticsAsync()
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var validationService = scope.ServiceProvider.GetService<InvariantValidationService>();
            
            if (validationService == null)
            {
                return new ViolationStatistics { GeneratedAt = DateTime.UtcNow };
            }

            var globalResult = await validationService.ValidateAllAsync();
            var summary = globalResult.GetSummary();

            return new ViolationStatistics
            {
                GeneratedAt = DateTime.UtcNow,
                TotalViolations = summary.TotalViolations,
                CriticalViolations = summary.CriticalViolations,
                ErrorViolations = summary.ErrorViolations,
                WarningViolations = summary.WarningViolations,
                InfoViolations = summary.InfoViolations,
                OverallHealthScore = 1.0 - (double)summary.TotalViolations / Math.Max(summary.TotalInvariants, 1),
                ConceptTypeBreakdown = new Dictionary<string, int>
                {
                    ["IconicChords"] = globalResult.IconicChordResults.Values.Sum(r => r.Failures.Count()),
                    ["ChordProgressions"] = globalResult.ChordProgressionResults.Values.Sum(r => r.Failures.Count()),
                    ["GuitarTechniques"] = globalResult.GuitarTechniqueResults.Values.Sum(r => r.Failures.Count()),
                    ["SpecializedTunings"] = globalResult.SpecializedTuningResults.Values.Sum(r => r.Failures.Count())
                }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating violation statistics");
            return new ViolationStatistics 
            { 
                GeneratedAt = DateTime.UtcNow,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Get recent violation events
    /// NOTE: With Channel-based architecture, we process violations immediately.
    /// This method is kept for backward compatibility but returns empty list.
    /// Consider using real-time monitoring via SignalR instead.
    /// </summary>
    public List<InvariantViolationEvent> GetRecentViolations(int maxCount = 50)
    {
        logger.LogWarning("GetRecentViolations called but Channel-based architecture processes violations immediately");
        return new List<InvariantViolationEvent>();
    }

    /// <summary>
    /// Clear violation queue
    /// NOTE: With Channel-based architecture, violations are processed immediately.
    /// This method is kept for backward compatibility but does nothing.
    /// </summary>
    public void ClearViolationQueue()
    {
        logger.LogInformation("ClearViolationQueue called (no-op with Channel-based architecture)");
    }

    /// <summary>
    /// Process a single violation event (called by Channel reader)
    /// </summary>
    private async Task ProcessViolationAsync(InvariantViolationEvent violation)
    {
        logger.LogDebug("Processing violation event: {InvariantName}", violation.Violation.InvariantName);

        try
        {
            // Process based on severity
            switch (violation.Violation.Severity)
            {
                case InvariantSeverity.Critical:
                    await ProcessCriticalViolations(new List<InvariantViolationEvent> { violation });
                    break;

                case InvariantSeverity.Error:
                    await ProcessErrorViolations(new List<InvariantViolationEvent> { violation });
                    break;

                case InvariantSeverity.Warning:
                    logger.LogWarning("Warning-level violation: {InvariantName} in {ConceptType}",
                                     violation.Violation.InvariantName,
                                     violation.ConceptType ?? violation.ConfigurationType);
                    break;

                case InvariantSeverity.Info:
                    logger.LogInformation("Info-level violation: {InvariantName}",
                                         violation.Violation.InvariantName);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing violation: {InvariantName}", violation.Violation.InvariantName);
        }
    }

    private async Task ProcessCriticalViolations(List<InvariantViolationEvent> violations)
    {
        logger.LogError("Processing {CriticalCount} critical invariant violations", violations.Count);

        foreach (var violation in violations)
        {
            logger.LogError("CRITICAL VIOLATION: {InvariantName} in {ConceptType} {ConceptName}: {ErrorMessage}",
                           violation.Violation.InvariantName,
                           violation.ConceptType ?? violation.ConfigurationType,
                           violation.ConceptName ?? "Configuration",
                           violation.Violation.ErrorMessage);
        }

        // Broadcast critical violations if service is available
        if (broadcastService != null)
        {
            var groupedViolations = violations.GroupBy(v => v.ConfigurationType ?? v.ConceptType ?? "Unknown");
            
            foreach (var group in groupedViolations)
            {
                await broadcastService.BroadcastConfigurationError(group.Key, 
                    $"Critical invariant violations: {group.Count()} violations detected");
            }
        }
    }

    private Task ProcessErrorViolations(List<InvariantViolationEvent> violations)
    {
        logger.LogError("Processing {ErrorCount} error-level invariant violations", violations.Count);

        foreach (var violation in violations)
        {
            logger.LogError("ERROR VIOLATION: {InvariantName} in {ConceptType} {ConceptName}: {ErrorMessage}",
                           violation.Violation.InvariantName,
                           violation.ConceptType ?? violation.ConfigurationType,
                           violation.ConceptName ?? "Configuration",
                           violation.Violation.ErrorMessage);
        }

        return Task.CompletedTask;
    }

    private Task<List<InvariantValidationResult>> ValidateConfigurationTypeAsync(InvariantValidationService validationService, string configurationType)
    {
        var violations = new List<InvariantValidationResult>();

        try
        {
            switch (configurationType.ToLowerInvariant())
            {
                case "iconicchords":
                    var chords = IconicChordsService.GetAllChords();
                    foreach (var chord in chords)
                    {
                        var result = validationService.ValidateIconicChord(chord);
                        violations.AddRange(result.Failures);
                    }
                    break;

                case "chordprogressions":
                    var progressions = ChordProgressionsService.GetAllProgressions();
                    foreach (var progression in progressions)
                    {
                        var result = validationService.ValidateChordProgression(progression);
                        violations.AddRange(result.Failures);
                    }
                    break;

                case "guitartechniques":
                    var techniques = GuitarTechniquesService.GetAllTechniques();
                    foreach (var technique in techniques)
                    {
                        var result = validationService.ValidateGuitarTechnique(technique);
                        violations.AddRange(result.Failures);
                    }
                    break;

                case "specializedtunings":
                    var tunings = SpecializedTuningsService.GetAllTunings();
                    foreach (var tuning in tunings)
                    {
                        var result = validationService.ValidateSpecializedTuning(tuning);
                        violations.AddRange(result.Failures);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating configuration type {ConfigurationType}", configurationType);
        }

        return Task.FromResult(violations);
    }
}

/// <summary>
/// Event representing an invariant violation
/// </summary>
public class InvariantViolationEvent
{
    public string? ConfigurationType { get; set; }
    public string? FileName { get; set; }
    public string? ConceptName { get; set; }
    public string? ConceptType { get; set; }
    public InvariantValidationResult Violation { get; set; } = new();
    public DateTime DetectedAt { get; set; }
    public bool IsProcessed { get; set; }
    public DateTime? ProcessedAt { get; set; }
}

/// <summary>
/// Statistics about invariant violations
/// </summary>
public class ViolationStatistics
{
    public DateTime GeneratedAt { get; set; }
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public int ErrorViolations { get; set; }
    public int WarningViolations { get; set; }
    public int InfoViolations { get; set; }
    public double OverallHealthScore { get; set; }
    public Dictionary<string, int> ConceptTypeBreakdown { get; set; } = [];
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Real-time violation monitoring configuration
/// </summary>
public class ViolationMonitoringConfig
{
    public bool EnableRealTimeMonitoring { get; set; } = true;
    public bool BroadcastCriticalViolations { get; set; } = true;
    public bool BroadcastErrorViolations { get; set; } = false;
    public int MaxQueueSize { get; set; } = 1000;
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(5);
    public List<string> MonitoredConceptTypes { get; set; } = ["IconicChords", "ChordProgressions", "GuitarTechniques", "SpecializedTunings"];
    public List<InvariantSeverity> AlertSeverities { get; set; } = [InvariantSeverity.Critical, InvariantSeverity.Error];
}
