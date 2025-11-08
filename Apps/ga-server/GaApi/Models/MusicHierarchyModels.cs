namespace GaApi.Models;

/// <summary>
///     Supported hierarchy levels exposed through the GraphQL API.
/// </summary>
public enum MusicHierarchyLevel
{
    SetClass,
    ForteNumber,
    PrimeForm,
    Chord,
    ChordVoicing,
    Scale
}

/// <summary>
///     Metadata for a hierarchy level used by the UI.
/// </summary>
public sealed record MusicHierarchyLevelInfo
{
    public MusicHierarchyLevel Level { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public int TotalItems { get; init; }
    public string PrimaryMetric { get; init; } = string.Empty;
    public IReadOnlyList<string> Highlights { get; init; } = Array.Empty<string>();
}

/// <summary>
///     Represents a single node in the music hierarchy graph.
/// </summary>
public sealed record MusicHierarchyItem
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public MusicHierarchyLevel Level { get; init; }
    public string Category { get; init; } = string.Empty;
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
}
