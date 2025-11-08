namespace GaApi.Services.ChordQuery;

using System.Diagnostics;
using Models;

/// <summary>
///     Interface for chord query executor
/// </summary>
public interface IChordQueryExecutor
{
    /// <summary>
    ///     Executes a chord query plan and returns results
    /// </summary>
    Task<(IEnumerable<ChordInContext> chords, ChordQueryStats stats)> ExecuteAsync(ChordQueryPlan plan);
}

/// <summary>
///     Chord query executor - executes query plans and invokes generators
/// </summary>
public class ChordQueryExecutor(
    IContextualChordService chordService,
    ICachingService cache,
    ILogger<ChordQueryExecutor> logger)
    : IChordQueryExecutor
{
    private readonly IContextualChordService _chordService = chordService;

    public async Task<(IEnumerable<ChordInContext> chords, ChordQueryStats stats)> ExecuteAsync(ChordQueryPlan plan)
    {
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Executing query plan: {Plan}", plan.GetDescription());

        // Try to get from cache first
        var cachedResult = await cache.GetOrCreateRegularAsync(plan.CacheKey, async () =>
        {
            // Generate chords based on plan
            var generated = await GenerateChordsAsync(plan);

            // Apply filters
            var filtered = ApplyFilters(generated, plan);

            // Apply limit
            var limited = filtered.Take(plan.Query.Filters.Limit).ToList();

            return limited;
        });

        stopwatch.Stop();

        var stats = new ChordQueryStats
        {
            ChordsGenerated = cachedResult.Count(), // This is after all processing
            ChordsAfterFiltering = cachedResult.Count(),
            ChordsReturned = cachedResult.Count(),
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            FromCache = false, // We don't track this yet
            CacheKey = plan.CacheKey
        };

        logger.LogInformation(
            "Query executed in {Ms}ms, returned {Count} chords",
            stats.ExecutionTimeMs,
            stats.ChordsReturned);

        return (cachedResult, stats);
    }

    /// <summary>
    ///     Generates chords by invoking the generators specified in the plan
    /// </summary>
    private async Task<IEnumerable<ChordInContext>> GenerateChordsAsync(ChordQueryPlan plan)
    {
        var allChords = new List<ChordInContext>();
        var query = plan.Query;

        foreach (var generatorType in plan.GeneratorsToInvoke)
        {
            logger.LogDebug("Invoking generator: {Generator}", generatorType);

            var chords = generatorType switch
            {
                ChordGeneratorType.Diatonic => await GenerateDiatonicChordsAsync(query),
                ChordGeneratorType.Borrowed => await GenerateBorrowedChordsAsync(query),
                ChordGeneratorType.SecondaryDominants => await GenerateSecondaryDominantsAsync(query),
                ChordGeneratorType.SecondaryTwoFive => await GenerateSecondaryTwoFiveAsync(query),
                _ => throw new InvalidOperationException($"Unknown generator type: {generatorType}")
            };

            allChords.AddRange(chords);
            logger.LogDebug("Generator {Generator} produced {Count} chords", generatorType, chords.Count());
        }

        logger.LogInformation("Total chords generated: {Count}", allChords.Count);
        return allChords;
    }

    /// <summary>
    ///     Applies filters specified in the plan
    /// </summary>
    private IEnumerable<ChordInContext> ApplyFilters(IEnumerable<ChordInContext> chords, ChordQueryPlan plan)
    {
        var result = chords;

        foreach (var filterType in plan.FiltersToApply)
        {
            logger.LogDebug("Applying filter: {Filter}", filterType);

            result = filterType switch
            {
                ChordFilterType.MinCommonality => result.Where(c => c.Commonality >= plan.Query.Filters.MinCommonality),
                ChordFilterType.NaturallyOccurring => result.Where(c => c.IsNaturallyOccurring),
                _ => result
            };
        }

        return result;
    }

    // Delegate to existing service methods
    // These will be refactored to be internal generator methods later

    private async Task<IEnumerable<ChordInContext>> GenerateDiatonicChordsAsync(ChordQuery query)
    {
        // For now, we'll need to call the service's internal methods
        // This is a temporary bridge until we refactor the service
        return await Task.FromResult(Enumerable.Empty<ChordInContext>());
    }

    private async Task<IEnumerable<ChordInContext>> GenerateBorrowedChordsAsync(ChordQuery query)
    {
        return await Task.FromResult(Enumerable.Empty<ChordInContext>());
    }

    private async Task<IEnumerable<ChordInContext>> GenerateSecondaryDominantsAsync(ChordQuery query)
    {
        return await Task.FromResult(Enumerable.Empty<ChordInContext>());
    }

    private async Task<IEnumerable<ChordInContext>> GenerateSecondaryTwoFiveAsync(ChordQuery query)
    {
        return await Task.FromResult(Enumerable.Empty<ChordInContext>());
    }
}
