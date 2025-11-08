using Microsoft.Extensions.Options;
using GA.Business.Core.Configuration;
using GA.Business.Core.Invariants;
using GA.Business.Core.Analytics;
using System.Diagnostics;

namespace GA.Business.Core.Services;

/// <summary>
/// Service for validating invariants across all musical concepts
/// </summary>
public class InvariantValidationService(
    ILogger<InvariantValidationService> logger,
    InvariantConfigurationLoader configurationLoader,
    ConfigurableInvariantFactory configurableFactory,
    IOptions<InvariantValidationSettings> settings,
    InvariantAnalyticsService? analyticsService = null)
{
    protected readonly ILogger<InvariantValidationService> Logger = logger;
    private readonly InvariantValidationSettings _settings = settings.Value;
    private InvariantConfiguration? _currentConfiguration;

    /// <summary>
    /// Initialize the service with configuration
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            _currentConfiguration = await configurationLoader.LoadConfigurationAsync();
            Logger.LogInformation("Invariant validation service initialized with configuration");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize invariant validation service with configuration, falling back to hardcoded invariants");
            _currentConfiguration = null;
        }
    }

    /// <summary>
    /// Validates all invariants for an IconicChord
    /// </summary>
    public virtual CompositeInvariantValidationResult ValidateIconicChord(IconicChordDefinition chord)
    {
        Logger.LogDebug("Validating invariants for IconicChord: {ChordName}", chord.Name);

        var results = new List<InvariantValidationResult>();
        var invariants = GetInvariantsForType<IconicChordDefinition>();

        foreach (var invariant in invariants)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var result = invariant.Validate(chord);
                stopwatch.Stop();
                results.Add(result);

                // Record analytics
                analyticsService?.RecordValidation(
                    invariant.InvariantName,
                    nameof(IconicChordDefinition),
                    result.IsValid,
                    stopwatch.Elapsed,
                    result.ErrorMessage);

                if (!result.IsValid)
                {
                    Logger.LogWarning("Invariant violation in IconicChord {ChordName}: {InvariantName} - {ErrorMessage}",
                                     chord.Name, invariant.InvariantName, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Logger.LogError(ex, "Error validating invariant {InvariantName} for IconicChord {ChordName}",
                               invariant.InvariantName, chord.Name);

                var errorResult = new InvariantValidationResult
                {
                    IsValid = false,
                    InvariantName = invariant.InvariantName,
                    Severity = InvariantSeverity.Critical,
                    Category = invariant.Category,
                    ErrorMessage = $"Exception during validation: {ex.Message}"
                };

                results.Add(errorResult);

                // Record analytics for exception
                analyticsService?.RecordValidation(
                    invariant.InvariantName,
                    nameof(IconicChordDefinition),
                    false,
                    stopwatch.Elapsed,
                    ex.Message);
            }
        }

        var compositeResult = new CompositeInvariantValidationResult { Results = results };

        Logger.LogDebug("IconicChord {ChordName} validation completed: {PassedCount}/{TotalCount} invariants passed",
                       chord.Name, compositeResult.Successes.Count(), compositeResult.Results.Count);

        return compositeResult;
    }

    /// <summary>
    /// Get invariants for a specific type (configuration-driven or fallback to hardcoded)
    /// </summary>
    private IEnumerable<IInvariant<T>> GetInvariantsForType<T>()
    {
        if (_currentConfiguration != null)
        {
            var typeName = typeof(T).Name;
            var groupKey = typeName.Replace("Definition", "").ToLowerInvariant() + "s";

            if (_currentConfiguration.InvariantGroups.TryGetValue(groupKey, out var groupDefinition))
            {
                return configurableFactory.CreateInvariants<T>(groupDefinition);
            }
        }

        // Fallback to hardcoded invariants
        return GetHardcodedInvariants<T>();
    }

    /// <summary>
    /// Get hardcoded invariants as fallback
    /// </summary>
    private static IEnumerable<IInvariant<T>> GetHardcodedInvariants<T>()
    {
        return typeof(T).Name switch
        {
            nameof(IconicChordDefinition) => IconicChordInvariants.GetAll().Cast<IInvariant<T>>(),
            nameof(ChordProgressionDefinition) => ChordProgressionInvariants.GetAll().Cast<IInvariant<T>>(),
            nameof(GuitarTechniqueDefinition) => GuitarTechniqueInvariants.GetAll().Cast<IInvariant<T>>(),
            nameof(SpecializedTuningDefinition) => SpecializedTuningInvariants.GetAll().Cast<IInvariant<T>>(),
            _ => []
        };
    }

    /// <summary>
    /// Validates all invariants for a ChordProgression
    /// </summary>
    public virtual CompositeInvariantValidationResult ValidateChordProgression(ChordProgressionDefinition progression)
    {
        Logger.LogDebug("Validating invariants for ChordProgression: {ProgressionName}", progression.Name);

        var results = new List<InvariantValidationResult>();
        var invariants = ChordProgressionInvariants.GetAll();

        foreach (var invariant in invariants)
        {
            try
            {
                var result = invariant.Validate(progression);
                results.Add(result);

                if (!result.IsValid)
                {
                    Logger.LogWarning("Invariant violation in ChordProgression {ProgressionName}: {InvariantName} - {ErrorMessage}", 
                                     progression.Name, invariant.InvariantName, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error validating invariant {InvariantName} for ChordProgression {ProgressionName}", 
                               invariant.InvariantName, progression.Name);
                
                results.Add(new InvariantValidationResult
                {
                    IsValid = false,
                    InvariantName = invariant.InvariantName,
                    Severity = InvariantSeverity.Critical,
                    Category = invariant.Category,
                    ErrorMessage = $"Exception during validation: {ex.Message}"
                });
            }
        }

        var compositeResult = new CompositeInvariantValidationResult { Results = results };
        
        Logger.LogDebug("ChordProgression {ProgressionName} validation completed: {PassedCount}/{TotalCount} invariants passed", 
                       progression.Name, compositeResult.Successes.Count(), compositeResult.Results.Count);

        return compositeResult;
    }

    /// <summary>
    /// Validates all invariants for a GuitarTechnique
    /// </summary>
    public virtual CompositeInvariantValidationResult ValidateGuitarTechnique(GuitarTechniqueDefinition technique)
    {
        Logger.LogDebug("Validating invariants for GuitarTechnique: {TechniqueName}", technique.Name);

        var results = new List<InvariantValidationResult>();
        var invariants = GuitarTechniqueInvariants.GetAll();

        foreach (var invariant in invariants)
        {
            try
            {
                var result = invariant.Validate(technique);
                results.Add(result);

                if (!result.IsValid)
                {
                    Logger.LogWarning("Invariant violation in GuitarTechnique {TechniqueName}: {InvariantName} - {ErrorMessage}", 
                                     technique.Name, invariant.InvariantName, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error validating invariant {InvariantName} for GuitarTechnique {TechniqueName}", 
                               invariant.InvariantName, technique.Name);
                
                results.Add(new InvariantValidationResult
                {
                    IsValid = false,
                    InvariantName = invariant.InvariantName,
                    Severity = InvariantSeverity.Critical,
                    Category = invariant.Category,
                    ErrorMessage = $"Exception during validation: {ex.Message}"
                });
            }
        }

        var compositeResult = new CompositeInvariantValidationResult { Results = results };
        
        Logger.LogDebug("GuitarTechnique {TechniqueName} validation completed: {PassedCount}/{TotalCount} invariants passed", 
                       technique.Name, compositeResult.Successes.Count(), compositeResult.Results.Count);

        return compositeResult;
    }

    /// <summary>
    /// Validates all invariants for a SpecializedTuning
    /// </summary>
    public virtual CompositeInvariantValidationResult ValidateSpecializedTuning(SpecializedTuningDefinition tuning)
    {
        Logger.LogDebug("Validating invariants for SpecializedTuning: {TuningName}", tuning.Name);

        var results = new List<InvariantValidationResult>();
        var invariants = SpecializedTuningInvariants.GetAll();

        foreach (var invariant in invariants)
        {
            try
            {
                var result = invariant.Validate(tuning);
                results.Add(result);

                if (!result.IsValid)
                {
                    Logger.LogWarning("Invariant violation in SpecializedTuning {TuningName}: {InvariantName} - {ErrorMessage}", 
                                     tuning.Name, invariant.InvariantName, result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error validating invariant {InvariantName} for SpecializedTuning {TuningName}", 
                               invariant.InvariantName, tuning.Name);
                
                results.Add(new InvariantValidationResult
                {
                    IsValid = false,
                    InvariantName = invariant.InvariantName,
                    Severity = InvariantSeverity.Critical,
                    Category = invariant.Category,
                    ErrorMessage = $"Exception during validation: {ex.Message}"
                });
            }
        }

        var compositeResult = new CompositeInvariantValidationResult { Results = results };
        
        Logger.LogDebug("SpecializedTuning {TuningName} validation completed: {PassedCount}/{TotalCount} invariants passed", 
                       tuning.Name, compositeResult.Successes.Count(), compositeResult.Results.Count);

        return compositeResult;
    }

    /// <summary>
    /// Validates all musical concepts in the knowledge base
    /// </summary>
    public Task<GlobalValidationResult> ValidateAllAsync()
    {
        Logger.LogInformation("Starting global validation of all musical concepts");

        var globalResult = new GlobalValidationResult
        {
            StartedAt = DateTime.UtcNow
        };

        try
        {
            // Validate IconicChords
            var iconicChords = IconicChordsService.GetAllChords();
            foreach (var chord in iconicChords)
            {
                var result = ValidateIconicChord(chord);
                globalResult.IconicChordResults.Add(chord.Name, result);
            }

            // Validate ChordProgressions
            var progressions = ChordProgressionsService.GetAllProgressions();
            foreach (var progression in progressions)
            {
                var result = ValidateChordProgression(progression);
                globalResult.ChordProgressionResults.Add(progression.Name, result);
            }

            // Validate GuitarTechniques
            var techniques = GuitarTechniquesService.GetAllTechniques();
            foreach (var technique in techniques)
            {
                var result = ValidateGuitarTechnique(technique);
                globalResult.GuitarTechniqueResults.Add(technique.Name, result);
            }

            // Validate SpecializedTunings
            var tunings = SpecializedTuningsService.GetAllTunings();
            foreach (var tuning in tunings)
            {
                var result = ValidateSpecializedTuning(tuning);
                globalResult.SpecializedTuningResults.Add(tuning.Name, result);
            }

            globalResult.CompletedAt = DateTime.UtcNow;
            globalResult.IsCompleted = true;

            var summary = globalResult.GetSummary();
            Logger.LogInformation("Global validation completed: {TotalConcepts} concepts validated, {TotalViolations} violations found", 
                                 summary.TotalConcepts, summary.TotalViolations);

            if (summary.CriticalViolations > 0)
            {
                Logger.LogError("Found {CriticalCount} critical invariant violations", summary.CriticalViolations);
            }

            return Task.FromResult(globalResult);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during global validation");
            globalResult.CompletedAt = DateTime.UtcNow;
            globalResult.IsCompleted = false;
            globalResult.ErrorMessage = ex.Message;
            return Task.FromResult(globalResult);
        }
    }

    /// <summary>
    /// Validates specific concepts by name and type
    /// </summary>
    public CompositeInvariantValidationResult ValidateConcept(string conceptName, string conceptType)
    {
        Logger.LogDebug("Validating concept: {ConceptName} of type {ConceptType}", conceptName, conceptType);

        return conceptType.ToLowerInvariant() switch
        {
            "iconicchord" => ValidateIconicChordByName(conceptName),
            "chordprogression" => ValidateChordProgressionByName(conceptName),
            "guitartechnique" => ValidateGuitarTechniqueByName(conceptName),
            "specializedtuning" => ValidateSpecializedTuningByName(conceptName),
            _ => throw new ArgumentException($"Unknown concept type: {conceptType}")
        };
    }

    /// <summary>
    /// Gets validation statistics for all concepts
    /// </summary>
    public async Task<ValidationStatistics> GetValidationStatisticsAsync()
    {
        Logger.LogDebug("Generating validation statistics");

        var globalResult = await ValidateAllAsync();
        var summary = globalResult.GetSummary();

        var statistics = new ValidationStatistics
        {
            GeneratedAt = DateTime.UtcNow,
            TotalConcepts = summary.TotalConcepts,
            TotalInvariants = summary.TotalInvariants,
            TotalViolations = summary.TotalViolations,
            CriticalViolations = summary.CriticalViolations,
            ErrorViolations = summary.ErrorViolations,
            WarningViolations = summary.WarningViolations,
            InfoViolations = summary.InfoViolations,
            OverallSuccessRate = summary.OverallSuccessRate
        };

        // Add concept-specific statistics
        statistics.ConceptStatistics["IconicChords"] = new ConceptValidationStatistics
        {
            TotalConcepts = globalResult.IconicChordResults.Count,
            ValidConcepts = globalResult.IconicChordResults.Values.Count(r => r.IsValid),
            TotalViolations = globalResult.IconicChordResults.Values.Sum(r => r.Failures.Count())
        };

        statistics.ConceptStatistics["ChordProgressions"] = new ConceptValidationStatistics
        {
            TotalConcepts = globalResult.ChordProgressionResults.Count,
            ValidConcepts = globalResult.ChordProgressionResults.Values.Count(r => r.IsValid),
            TotalViolations = globalResult.ChordProgressionResults.Values.Sum(r => r.Failures.Count())
        };

        statistics.ConceptStatistics["GuitarTechniques"] = new ConceptValidationStatistics
        {
            TotalConcepts = globalResult.GuitarTechniqueResults.Count,
            ValidConcepts = globalResult.GuitarTechniqueResults.Values.Count(r => r.IsValid),
            TotalViolations = globalResult.GuitarTechniqueResults.Values.Sum(r => r.Failures.Count())
        };

        statistics.ConceptStatistics["SpecializedTunings"] = new ConceptValidationStatistics
        {
            TotalConcepts = globalResult.SpecializedTuningResults.Count,
            ValidConcepts = globalResult.SpecializedTuningResults.Values.Count(r => r.IsValid),
            TotalViolations = globalResult.SpecializedTuningResults.Values.Sum(r => r.Failures.Count())
        };

        return statistics;
    }

    // Private helper methods
    private CompositeInvariantValidationResult ValidateIconicChordByName(string chordName)
    {
        var chord = IconicChordsService.FindChordByName(chordName);
        if (chord == null)
        {
            return new CompositeInvariantValidationResult
            {
                Results = [new InvariantValidationResult
                {
                    IsValid = false,
                    InvariantName = "ConceptExists",
                    Severity = InvariantSeverity.Error,
                    Category = "Existence",
                    ErrorMessage = $"IconicChord '{chordName}' not found"
                }]
            };
        }

        return ValidateIconicChord(chord);
    }

    private CompositeInvariantValidationResult ValidateChordProgressionByName(string progressionName)
    {
        var progression = ChordProgressionsService.FindProgressionByName(progressionName);
        if (progression == null)
        {
            return new CompositeInvariantValidationResult
            {
                Results = [new InvariantValidationResult
                {
                    IsValid = false,
                    InvariantName = "ConceptExists",
                    Severity = InvariantSeverity.Error,
                    Category = "Existence",
                    ErrorMessage = $"ChordProgression '{progressionName}' not found"
                }]
            };
        }

        return ValidateChordProgression(progression);
    }

    private CompositeInvariantValidationResult ValidateGuitarTechniqueByName(string techniqueName)
    {
        var technique = GuitarTechniquesService.FindTechniqueByName(techniqueName);
        if (technique == null)
        {
            return new CompositeInvariantValidationResult
            {
                Results = [new InvariantValidationResult
                {
                    IsValid = false,
                    InvariantName = "ConceptExists",
                    Severity = InvariantSeverity.Error,
                    Category = "Existence",
                    ErrorMessage = $"GuitarTechnique '{techniqueName}' not found"
                }]
            };
        }

        return ValidateGuitarTechnique(technique);
    }

    private CompositeInvariantValidationResult ValidateSpecializedTuningByName(string tuningName)
    {
        var tuning = SpecializedTuningsService.FindTuningByName(tuningName);
        if (tuning == null)
        {
            return new CompositeInvariantValidationResult
            {
                Results = [new InvariantValidationResult
                {
                    IsValid = false,
                    InvariantName = "ConceptExists",
                    Severity = InvariantSeverity.Error,
                    Category = "Existence",
                    ErrorMessage = $"SpecializedTuning '{tuningName}' not found"
                }]
            };
        }

        return ValidateSpecializedTuning(tuning);
    }
}

/// <summary>
/// Global validation result for all musical concepts
/// </summary>
public class GlobalValidationResult
{
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsCompleted { get; set; }
    public string? ErrorMessage { get; set; }

    public Dictionary<string, CompositeInvariantValidationResult> IconicChordResults { get; set; } = [];
    public Dictionary<string, CompositeInvariantValidationResult> ChordProgressionResults { get; set; } = [];
    public Dictionary<string, CompositeInvariantValidationResult> GuitarTechniqueResults { get; set; } = [];
    public Dictionary<string, CompositeInvariantValidationResult> SpecializedTuningResults { get; set; } = [];

    public TimeSpan Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : TimeSpan.Zero;

    public GlobalValidationSummary GetSummary()
    {
        var allResults = IconicChordResults.Values
            .Concat(ChordProgressionResults.Values)
            .Concat(GuitarTechniqueResults.Values)
            .Concat(SpecializedTuningResults.Values)
            .ToList();

        var allFailures = allResults.SelectMany(r => r.Failures).ToList();

        return new GlobalValidationSummary
        {
            TotalConcepts = IconicChordResults.Count + ChordProgressionResults.Count +
                          GuitarTechniqueResults.Count + SpecializedTuningResults.Count,
            ValidConcepts = allResults.Count(r => r.IsValid),
            TotalInvariants = allResults.Sum(r => r.Results.Count),
            TotalViolations = allFailures.Count,
            CriticalViolations = allFailures.Count(f => f.Severity == InvariantSeverity.Critical),
            ErrorViolations = allFailures.Count(f => f.Severity == InvariantSeverity.Error),
            WarningViolations = allFailures.Count(f => f.Severity == InvariantSeverity.Warning),
            InfoViolations = allFailures.Count(f => f.Severity == InvariantSeverity.Info),
            OverallSuccessRate = allResults.Any() ? (double)allResults.Count(r => r.IsValid) / allResults.Count : 1.0,
            Duration = Duration,
            GeneratedAt = CompletedAt ?? DateTime.UtcNow
        };
    }
}

/// <summary>
/// Summary of global validation results
/// </summary>
public class GlobalValidationSummary
{
    public int TotalConcepts { get; set; }
    public int ValidConcepts { get; set; }
    public int TotalInvariants { get; set; }
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public int ErrorViolations { get; set; }
    public int WarningViolations { get; set; }
    public int InfoViolations { get; set; }
    public double OverallSuccessRate { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Validation statistics for the entire system
/// </summary>
public class ValidationStatistics
{
    public DateTime GeneratedAt { get; set; }
    public int TotalConcepts { get; set; }
    public int TotalInvariants { get; set; }
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public int ErrorViolations { get; set; }
    public int WarningViolations { get; set; }
    public int InfoViolations { get; set; }
    public double OverallSuccessRate { get; set; }
    public Dictionary<string, ConceptValidationStatistics> ConceptStatistics { get; set; } = [];
}

/// <summary>
/// Validation statistics for a specific concept type
/// </summary>
public class ConceptValidationStatistics
{
    public int TotalConcepts { get; set; }
    public int ValidConcepts { get; set; }
    public int TotalViolations { get; set; }
    public double SuccessRate => TotalConcepts > 0 ? (double)ValidConcepts / TotalConcepts : 1.0;
}
