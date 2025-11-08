namespace GA.Business.AI.AI;

using Core.Atonal;
using Core.Fretboard.Shapes;
using JetBrains.Annotations;

/// <summary>
///     Service for Redis vector similarity search and AI features
/// </summary>
[PublicAPI]
public interface IRedisVectorService
{
    /// <summary>
    ///     Index all pitch-class sets with ICV vectors
    /// </summary>
    Task IndexPitchClassSetsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Index fretboard shapes with multi-dimensional embeddings
    /// </summary>
    Task IndexShapesAsync(
        IEnumerable<FretboardShape> shapes,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Find pitch-class sets similar to the given ICV
    /// </summary>
    /// <param name="icv">Interval-class vector</param>
    /// <param name="maxResults">Maximum number of results</param>
    /// <param name="maxDistance">Maximum L2 distance</param>
    /// <returns>List of similar pitch-class sets with distances</returns>
    Task<IEnumerable<(PitchClassSet Set, double Distance)>> FindSimilarPitchClassSetsAsync(
        IntervalClassVector icv,
        int maxResults = 10,
        double maxDistance = 5.0
    );

    /// <summary>
    ///     Find fretboard shapes similar to the given shape
    /// </summary>
    /// <param name="shape">Reference shape</param>
    /// <param name="options">Search options</param>
    /// <returns>List of similar shapes with distances</returns>
    Task<IEnumerable<(FretboardShape Shape, double Distance)>> FindSimilarShapesAsync(
        FretboardShape shape,
        ShapeSearchOptions options
    );

    /// <summary>
    ///     Semantic search for shapes using natural language
    /// </summary>
    /// <param name="query">Natural language query (e.g., "easy C major box shape")</param>
    /// <param name="maxResults">Maximum number of results</param>
    /// <returns>List of matching shapes</returns>
    Task<IEnumerable<FretboardShape>> SearchShapesAsync(
        string query,
        int maxResults = 10
    );

    /// <summary>
    ///     Cache heat map with TTL
    /// </summary>
    Task CacheHeatMapAsync(
        string shapeId,
        string optionsHash,
        double[,] heatMap,
        TimeSpan ttl
    );

    /// <summary>
    ///     Get cached heat map
    /// </summary>
    Task<double[,]?> GetCachedHeatMapAsync(
        string shapeId,
        string optionsHash
    );

    /// <summary>
    ///     Store user session data
    /// </summary>
    Task StoreUserSessionAsync(
        string userId,
        UserSession session,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Get user session data
    /// </summary>
    Task<UserSession?> GetUserSessionAsync(
        string userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     Update practice history for a user
    /// </summary>
    Task UpdatePracticeHistoryAsync(
        string userId,
        PracticeEntry entry,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
///     Options for shape search
/// </summary>
[PublicAPI]
public sealed record ShapeSearchOptions
{
    /// <summary>
    ///     Maximum number of results
    /// </summary>
    public int MaxResults { get; init; } = 10;

    /// <summary>
    ///     Maximum vector distance
    /// </summary>
    public double MaxDistance { get; init; } = 5.0;

    /// <summary>
    ///     Filter by diagness range (0-1)
    /// </summary>
    public (double Min, double Max)? DiagnessRange { get; init; }

    /// <summary>
    ///     Filter by ergonomics range (0-1)
    /// </summary>
    public (double Min, double Max)? ErgonomicsRange { get; init; }

    /// <summary>
    ///     Filter by span range (frets)
    /// </summary>
    public (int Min, int Max)? SpanRange { get; init; }

    /// <summary>
    ///     Filter by fret range
    /// </summary>
    public (int Min, int Max)? FretRange { get; init; }

    /// <summary>
    ///     Filter by tags
    /// </summary>
    public Dictionary<string, string>? Tags { get; init; }
}

/// <summary>
///     User session data for personalization
/// </summary>
[PublicAPI]
public sealed record UserSession
{
    /// <summary>
    ///     User ID
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    ///     Skill level (0-1, 0 = beginner, 1 = expert)
    /// </summary>
    public double SkillLevel { get; init; } = 0.5;

    /// <summary>
    ///     Preferred diagness (0 = box, 1 = diagonal)
    /// </summary>
    public double PreferredDiagness { get; init; } = 0.3;

    /// <summary>
    ///     Maximum comfortable span (frets)
    /// </summary>
    public int MaxComfortableSpan { get; init; } = 4;

    /// <summary>
    ///     Practice history
    /// </summary>
    public List<PracticeEntry> PracticeHistory { get; init; } = [];

    /// <summary>
    ///     Learned shape IDs
    /// </summary>
    public HashSet<string> LearnedShapes { get; init; } = [];

    /// <summary>
    ///     Last updated timestamp
    /// </summary>
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}

/// <summary>
///     Practice history entry
/// </summary>
[PublicAPI]
public sealed record PracticeEntry
{
    /// <summary>
    ///     Shape ID
    /// </summary>
    public required string ShapeId { get; init; }

    /// <summary>
    ///     Timestamp
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    ///     Success rate (0-1)
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    ///     Time spent practicing (seconds)
    /// </summary>
    public int TimeSpentSeconds { get; init; }

    /// <summary>
    ///     Difficulty rating (1-5)
    /// </summary>
    public int DifficultyRating { get; init; }
}
