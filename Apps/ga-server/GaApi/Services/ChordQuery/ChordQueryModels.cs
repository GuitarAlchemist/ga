namespace GaApi.Services.ChordQuery;

using GA.Business.Core.Tonal;
using GA.Business.Core.Tonal.Modes;
using Models;

/// <summary>
///     Type of chord query
/// </summary>
public enum ChordQueryType
{
    /// <summary>Query chords for a specific key</summary>
    Key,

    /// <summary>Query chords for a specific scale</summary>
    Scale,

    /// <summary>Query chords for a specific mode</summary>
    Mode
}

/// <summary>
///     Type of chord generator
/// </summary>
public enum ChordGeneratorType
{
    /// <summary>Generate diatonic chords (naturally occurring in the key/scale)</summary>
    Diatonic,

    /// <summary>Generate borrowed chords (modal interchange)</summary>
    Borrowed,

    /// <summary>Generate secondary dominants (V/x)</summary>
    SecondaryDominants,

    /// <summary>Generate secondary ii-V progressions</summary>
    SecondaryTwoFive
}

/// <summary>
///     Type of chord filter
/// </summary>
public enum ChordFilterType
{
    /// <summary>Filter by naturally occurring status</summary>
    NaturallyOccurring,

    /// <summary>Filter by minimum commonality</summary>
    MinCommonality,

    /// <summary>Filter by extension</summary>
    Extension,

    /// <summary>Filter by stacking type</summary>
    StackingType
}

/// <summary>
///     Immutable chord query object
/// </summary>
public record ChordQuery
{
    /// <summary>Type of query</summary>
    public required ChordQueryType QueryType { get; init; }

    /// <summary>Key for key-based queries</summary>
    public Key? Key { get; init; }

    /// <summary>Scale/Mode for scale/mode-based queries</summary>
    public ScaleMode? ScaleMode { get; init; }

    /// <summary>Filters to apply</summary>
    public required ChordFilters Filters { get; init; }

    /// <summary>
    ///     Validates the query
    /// </summary>
    public bool IsValid()
    {
        return QueryType switch
        {
            ChordQueryType.Key => Key != null,
            ChordQueryType.Scale => ScaleMode != null,
            ChordQueryType.Mode => ScaleMode != null,
            _ => false
        };
    }
}

/// <summary>
///     Execution plan for a chord query
/// </summary>
public record ChordQueryPlan
{
    /// <summary>Cache key for this query</summary>
    public required string CacheKey { get; init; }

    /// <summary>Generators to invoke in order</summary>
    public required IReadOnlyList<ChordGeneratorType> GeneratorsToInvoke { get; init; }

    /// <summary>Filters to apply after generation</summary>
    public required IReadOnlyList<ChordFilterType> FiltersToApply { get; init; }

    /// <summary>Original query</summary>
    public required ChordQuery Query { get; init; }

    /// <summary>
    ///     Gets a description of the execution plan for logging
    /// </summary>
    public string GetDescription()
    {
        var generators = string.Join(", ", GeneratorsToInvoke);
        var filters = string.Join(", ", FiltersToApply);
        return $"Generators: [{generators}], Filters: [{filters}]";
    }
}

/// <summary>
///     Statistics about query execution
/// </summary>
public record ChordQueryStats
{
    /// <summary>Number of chords generated</summary>
    public int ChordsGenerated { get; init; }

    /// <summary>Number of chords after filtering</summary>
    public int ChordsAfterFiltering { get; init; }

    /// <summary>Number of chords returned (after limit)</summary>
    public int ChordsReturned { get; init; }

    /// <summary>Execution time in milliseconds</summary>
    public long ExecutionTimeMs { get; init; }

    /// <summary>Whether result was from cache</summary>
    public bool FromCache { get; init; }

    /// <summary>Cache key used</summary>
    public string? CacheKey { get; init; }
}
