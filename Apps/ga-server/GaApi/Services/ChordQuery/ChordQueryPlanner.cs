namespace GaApi.Services.ChordQuery;

using Models;

/// <summary>
///     Interface for chord query planner
/// </summary>
public interface IChordQueryPlanner
{
    /// <summary>
    ///     Creates an optimal execution plan for a chord query
    /// </summary>
    ChordQueryPlan CreatePlan(ChordQuery query);
}

/// <summary>
///     Chord query planner - analyzes queries and creates optimal execution plans
/// </summary>
public class ChordQueryPlanner(ILogger<ChordQueryPlanner> logger) : IChordQueryPlanner
{
    public ChordQueryPlan CreatePlan(ChordQuery query)
    {
        if (!query.IsValid())
        {
            throw new ArgumentException("Invalid chord query", nameof(query));
        }

        logger.LogDebug("Creating execution plan for {QueryType} query", query.QueryType);

        // Determine which generators to invoke based on filters
        var generators = DetermineGenerators(query.Filters);

        // Determine which filters to apply after generation
        var filters = DetermineFilters(query.Filters);

        // Generate cache key
        var cacheKey = GenerateCacheKey(query);

        var plan = new ChordQueryPlan
        {
            CacheKey = cacheKey,
            GeneratorsToInvoke = generators,
            FiltersToApply = filters,
            Query = query
        };

        logger.LogDebug("Execution plan: {Plan}", plan.GetDescription());

        return plan;
    }

    /// <summary>
    ///     Determines which chord generators to invoke based on filters
    /// </summary>
    private List<ChordGeneratorType> DetermineGenerators(ChordFilters filters)
    {
        var generators = new List<ChordGeneratorType>();

        // Always include diatonic chords as the base
        generators.Add(ChordGeneratorType.Diatonic);

        // If OnlyNaturallyOccurring is true, ONLY generate diatonic chords
        // This takes precedence over all Include* flags
        if (filters.OnlyNaturallyOccurring)
        {
            logger.LogDebug("OnlyNaturallyOccurring=true, limiting to diatonic chords only");
            return generators;
        }

        // Otherwise, add generators based on Include* flags
        if (filters.IncludeBorrowedChords)
        {
            generators.Add(ChordGeneratorType.Borrowed);
            logger.LogDebug("Including borrowed chords");
        }

        if (filters.IncludeSecondaryDominants)
        {
            generators.Add(ChordGeneratorType.SecondaryDominants);
            logger.LogDebug("Including secondary dominants");
        }

        if (filters.IncludeSecondaryTwoFive)
        {
            generators.Add(ChordGeneratorType.SecondaryTwoFive);
            logger.LogDebug("Including secondary ii-V progressions");
        }

        return generators;
    }

    /// <summary>
    ///     Determines which filters to apply after generation
    /// </summary>
    private List<ChordFilterType> DetermineFilters(ChordFilters filters)
    {
        var filterTypes = new List<ChordFilterType>();

        // Note: OnlyNaturallyOccurring is handled by generator selection, not filtering
        // This is more efficient - we don't generate chords we'll filter out

        if (filters.MinCommonality > 0)
        {
            filterTypes.Add(ChordFilterType.MinCommonality);
        }

        // Extension and StackingType are handled during generation, not filtering
        // They determine what types of chords to generate

        return filterTypes;
    }

    /// <summary>
    ///     Generates a cache key for the query
    /// </summary>
    private string GenerateCacheKey(ChordQuery query)
    {
        var parts = new List<string>
        {
            query.QueryType.ToString().ToLower()
        };

        // Add key/scale/mode identifier
        switch (query.QueryType)
        {
            case ChordQueryType.Key:
                parts.Add(query.Key?.ToString() ?? "unknown");
                break;
            case ChordQueryType.Scale:
            case ChordQueryType.Mode:
                parts.Add(query.ScaleMode?.Name ?? "unknown");
                break;
        }

        // Add filter parameters
        var filters = query.Filters;
        parts.Add($"ext:{filters.Extension?.ToString() ?? "any"}");
        parts.Add($"stack:{filters.StackingType?.ToString() ?? "any"}");
        parts.Add($"nat:{filters.OnlyNaturallyOccurring}");
        parts.Add($"bor:{filters.IncludeBorrowedChords}");
        parts.Add($"sec:{filters.IncludeSecondaryDominants}");
        parts.Add($"ii-v:{filters.IncludeSecondaryTwoFive}");
        parts.Add($"min:{filters.MinCommonality:F2}");
        parts.Add($"lim:{filters.Limit}");

        return string.Join("_", parts);
    }
}
