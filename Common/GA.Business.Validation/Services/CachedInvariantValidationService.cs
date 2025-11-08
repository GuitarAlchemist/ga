using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using GA.Business.Core.Configuration;
using GA.Business.Core.Invariants;
using GA.Business.Core.Analytics;

namespace GA.Business.Core.Services;

/// <summary>
/// Cached version of the invariant validation service for improved performance
/// </summary>
public class CachedInvariantValidationService(
    ILogger<InvariantValidationService> logger,
    InvariantConfigurationLoader configurationLoader,
    ConfigurableInvariantFactory configurableFactory,
    IOptions<InvariantValidationSettings> settings,
    IMemoryCache cache,
    InvariantAnalyticsService? analyticsService = null)
    : InvariantValidationService(logger, configurationLoader, configurableFactory, settings, analyticsService)
{
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Validates IconicChord with caching
    /// </summary>
    public override CompositeInvariantValidationResult ValidateIconicChord(IconicChordDefinition chord)
    {
        var cacheKey = $"validation_iconic_chord_{chord.Name}_{GetChordHash(chord)}";
        
        if (cache.TryGetValue(cacheKey, out CompositeInvariantValidationResult? cachedResult))
        {
            return cachedResult!;
        }

        var result = base.ValidateIconicChord(chord);
        cache.Set(cacheKey, result, _cacheExpiration);
        
        return result;
    }

    /// <summary>
    /// Validates ChordProgression with caching
    /// </summary>
    public override CompositeInvariantValidationResult ValidateChordProgression(ChordProgressionDefinition progression)
    {
        var cacheKey = $"validation_chord_progression_{progression.Name}_{GetProgressionHash(progression)}";
        
        if (cache.TryGetValue(cacheKey, out CompositeInvariantValidationResult? cachedResult))
        {
            return cachedResult!;
        }

        var result = base.ValidateChordProgression(progression);
        cache.Set(cacheKey, result, _cacheExpiration);
        
        return result;
    }

    /// <summary>
    /// Validates GuitarTechnique with caching
    /// </summary>
    public override CompositeInvariantValidationResult ValidateGuitarTechnique(GuitarTechniqueDefinition technique)
    {
        var cacheKey = $"validation_guitar_technique_{technique.Name}_{GetTechniqueHash(technique)}";
        
        if (cache.TryGetValue(cacheKey, out CompositeInvariantValidationResult? cachedResult))
        {
            return cachedResult!;
        }

        var result = base.ValidateGuitarTechnique(technique);
        cache.Set(cacheKey, result, _cacheExpiration);
        
        return result;
    }

    /// <summary>
    /// Validates SpecializedTuning with caching
    /// </summary>
    public override CompositeInvariantValidationResult ValidateSpecializedTuning(SpecializedTuningDefinition tuning)
    {
        var cacheKey = $"validation_specialized_tuning_{tuning.Name}_{GetTuningHash(tuning)}";
        
        if (cache.TryGetValue(cacheKey, out CompositeInvariantValidationResult? cachedResult))
        {
            return cachedResult!;
        }

        var result = base.ValidateSpecializedTuning(tuning);
        cache.Set(cacheKey, result, _cacheExpiration);
        
        return result;
    }

    /// <summary>
    /// Clear validation cache
    /// </summary>
    public void ClearValidationCache()
    {
        if (cache is MemoryCache memoryCache)
        {
            memoryCache.Clear();
        }
    }

    /// <summary>
    /// Clear cache for specific concept type
    /// </summary>
    public void ClearValidationCache(string conceptType)
    {
        // Note: MemoryCache doesn't support pattern-based clearing
        // In production, consider using a distributed cache with pattern support
        ClearValidationCache();
    }

    private static string GetChordHash(IconicChordDefinition chord)
    {
        var hashInput = $"{chord.Name}_{chord.TheoreticalName}_{string.Join(",", chord.PitchClasses ?? [])}_{chord.Artist}_{chord.Song}";
        return hashInput.GetHashCode().ToString();
    }

    private static string GetProgressionHash(ChordProgressionDefinition progression)
    {
        var hashInput = $"{progression.Name}_{string.Join(",", progression.RomanNumerals ?? [])}_{progression.Category}_{progression.InKey}";
        return hashInput.GetHashCode().ToString();
    }

    private static string GetTechniqueHash(GuitarTechniqueDefinition technique)
    {
        var hashInput = $"{technique.Name}_{technique.Category}_{technique.Difficulty}_{technique.Description}";
        return hashInput.GetHashCode().ToString();
    }

    private static string GetTuningHash(SpecializedTuningDefinition tuning)
    {
        var hashInput = $"{tuning.Name}_{tuning.Category}_{string.Join(",", tuning.PitchClasses ?? [])}_{tuning.TuningPattern}";
        return hashInput.GetHashCode().ToString();
    }
}

/// <summary>
/// Performance monitoring for invariant validation
/// </summary>
public class InvariantValidationPerformanceMonitor(ILogger<InvariantValidationPerformanceMonitor> logger)
{
    private readonly Dictionary<string, List<TimeSpan>> _executionTimes = [];
    private readonly object _lock = new();

    /// <summary>
    /// Record execution time for an invariant
    /// </summary>
    public void RecordExecutionTime(string invariantName, TimeSpan executionTime)
    {
        lock (_lock)
        {
            if (!_executionTimes.ContainsKey(invariantName))
            {
                _executionTimes[invariantName] = [];
            }

            _executionTimes[invariantName].Add(executionTime);

            // Keep only last 100 measurements
            if (_executionTimes[invariantName].Count > 100)
            {
                _executionTimes[invariantName].RemoveAt(0);
            }
        }

        // Log slow invariants
        if (executionTime > TimeSpan.FromMilliseconds(100))
        {
            logger.LogWarning("Slow invariant execution: {InvariantName} took {ExecutionTime}ms", 
                             invariantName, executionTime.TotalMilliseconds);
        }
    }

    /// <summary>
    /// Get performance statistics for an invariant
    /// </summary>
    public InvariantPerformanceStats? GetPerformanceStats(string invariantName)
    {
        lock (_lock)
        {
            if (!_executionTimes.TryGetValue(invariantName, out var times) || !times.Any())
            {
                return null;
            }

            var totalMs = times.Select(t => t.TotalMilliseconds).ToList();
            
            return new InvariantPerformanceStats
            {
                InvariantName = invariantName,
                ExecutionCount = times.Count,
                AverageExecutionTime = TimeSpan.FromMilliseconds(totalMs.Average()),
                MinExecutionTime = TimeSpan.FromMilliseconds(totalMs.Min()),
                MaxExecutionTime = TimeSpan.FromMilliseconds(totalMs.Max()),
                MedianExecutionTime = TimeSpan.FromMilliseconds(GetMedian(totalMs)),
                P95ExecutionTime = TimeSpan.FromMilliseconds(GetPercentile(totalMs, 0.95)),
                P99ExecutionTime = TimeSpan.FromMilliseconds(GetPercentile(totalMs, 0.99))
            };
        }
    }

    /// <summary>
    /// Get performance statistics for all invariants
    /// </summary>
    public List<InvariantPerformanceStats> GetAllPerformanceStats()
    {
        lock (_lock)
        {
            return _executionTimes.Keys
                .Select(GetPerformanceStats)
                .Where(stats => stats != null)
                .Cast<InvariantPerformanceStats>()
                .OrderByDescending(stats => stats.AverageExecutionTime)
                .ToList();
        }
    }

    /// <summary>
    /// Clear performance data
    /// </summary>
    public void ClearPerformanceData()
    {
        lock (_lock)
        {
            _executionTimes.Clear();
        }
    }

    private static double GetMedian(List<double> values)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var count = sorted.Count;
        
        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        
        return sorted[count / 2];
    }

    private static double GetPercentile(List<double> values, double percentile)
    {
        var sorted = values.OrderBy(x => x).ToList();
        var index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
    }
}

/// <summary>
/// Performance statistics for an invariant
/// </summary>
public class InvariantPerformanceStats
{
    public string InvariantName { get; set; } = string.Empty;
    public int ExecutionCount { get; set; }
    public TimeSpan AverageExecutionTime { get; set; }
    public TimeSpan MinExecutionTime { get; set; }
    public TimeSpan MaxExecutionTime { get; set; }
    public TimeSpan MedianExecutionTime { get; set; }
    public TimeSpan P95ExecutionTime { get; set; }
    public TimeSpan P99ExecutionTime { get; set; }
}

/// <summary>
/// Configuration validation settings
/// </summary>
public class InvariantValidationSettings
{
    public bool EnableCaching { get; set; } = true;
    public TimeSpan CacheExpiration { get; set; } = TimeSpan.FromMinutes(15);
    public bool EnablePerformanceMonitoring { get; set; } = true;
    public bool EnableAsyncValidation { get; set; } = true;
    public int MaxConcurrentValidations { get; set; } = Environment.ProcessorCount * 2;
    public TimeSpan ValidationTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public List<string> DisabledInvariants { get; set; } = [];
    public Dictionary<InvariantSeverity, bool> EnabledSeverityLevels { get; set; } = new()
    {
        [InvariantSeverity.Info] = true,
        [InvariantSeverity.Warning] = true,
        [InvariantSeverity.Error] = true,
        [InvariantSeverity.Critical] = true
    };
}
