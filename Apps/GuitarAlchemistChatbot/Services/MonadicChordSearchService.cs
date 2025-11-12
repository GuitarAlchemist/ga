namespace GuitarAlchemistChatbot.Services;

using GA.Business.Core.Microservices;
using GA.Business.Core.Microservices.Microservices;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
///     Monadic chord search service using Try and Result monads for type-safe HTTP operations
/// </summary>
public class MonadicChordSearchService : MonadicServiceBase<MonadicChordSearchService>
{
    private readonly string _apiBaseUrl;
    private readonly IConfiguration _configuration;
    private readonly MonadicHttpClient _httpClient;

    public MonadicChordSearchService(
        IHttpClientFactory httpClientFactory,
        ILogger<MonadicChordSearchService> logger,
        ILogger<MonadicHttpClient> httpLogger,
        IMemoryCache cache,
        IConfiguration configuration)
        : base(logger, cache)
    {
        var httpClient = httpClientFactory.CreateClient("GuitarAlchemistApi");
        _httpClient = new MonadicHttpClient(httpClient, httpLogger);
        _configuration = configuration;
        _apiBaseUrl = _configuration["GuitarAlchemistApi:BaseUrl"] ?? "http://localhost:5000";
    }

    /// <summary>
    ///     Search for chords using natural language queries
    ///     Returns Result with fallback to demo data on failure
    /// </summary>
    public async Task<Result<List<ChordSearchResult>, SearchError>> SearchChordsAsync(
        string query,
        int limit = 10,
        int numCandidates = 100)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(query))
        {
            return new Result<List<ChordSearchResult>, SearchError>.Failure(
                new SearchError(SearchErrorType.ValidationError, "Query cannot be empty")
            );
        }

        if (limit <= 0 || limit > 100)
        {
            return new Result<List<ChordSearchResult>, SearchError>.Failure(
                new SearchError(SearchErrorType.ValidationError, "Limit must be between 1 and 100")
            );
        }

        Logger.LogInformation("Searching chords with query: {Query}", query);

        var url =
            $"{_apiBaseUrl}/api/VectorSearch/semantic?q={Uri.EscapeDataString(query)}&limit={limit}&numCandidates={numCandidates}";
        var tryResults = await _httpClient.GetAsync<List<ChordSearchResult>>(url);

        return tryResults.Match(
            onSuccess: results => new Result<List<ChordSearchResult>, SearchError>.Success(results),
            onFailure: ex =>
            {
                Logger.LogWarning(ex, "API call failed for query: {Query}. Falling back to demo data.", query);
                var demoResults = GetDemoChordResults(query, limit);
                return new Result<List<ChordSearchResult>, SearchError>.Success(demoResults);
            }
        );
    }

    /// <summary>
    ///     Find chords similar to a given chord
    ///     Returns Result with fallback to demo data on failure
    /// </summary>
    public async Task<Result<List<ChordSearchResult>, SearchError>> FindSimilarChordsAsync(
        int chordId,
        int limit = 10,
        int numCandidates = 100)
    {
        // Validate input
        if (chordId <= 0)
        {
            return new Result<List<ChordSearchResult>, SearchError>.Failure(
                new SearchError(SearchErrorType.ValidationError, "Chord ID must be positive")
            );
        }

        if (limit <= 0 || limit > 100)
        {
            return new Result<List<ChordSearchResult>, SearchError>.Failure(
                new SearchError(SearchErrorType.ValidationError, "Limit must be between 1 and 100")
            );
        }

        Logger.LogInformation("Finding similar chords for chord ID: {ChordId}", chordId);

        var url = $"{_apiBaseUrl}/api/VectorSearch/similar/{chordId}?limit={limit}&numCandidates={numCandidates}";
        var tryResults = await _httpClient.GetAsync<List<ChordSearchResult>>(url);

        return tryResults.Match(
            onSuccess: results => new Result<List<ChordSearchResult>, SearchError>.Success(results),
            onFailure: ex =>
            {
                Logger.LogWarning(ex, "API call failed for chord ID: {ChordId}. Falling back to demo data.", chordId);
                var demoResults = GetDemoSimilarChords(chordId, limit);
                return new Result<List<ChordSearchResult>, SearchError>.Success(demoResults);
            }
        );
    }

    /// <summary>
    ///     Get chord details by ID
    ///     Returns Option monad - Some if found, None if not found
    /// </summary>
    public async Task<Option<ChordSearchResult>> GetChordByIdAsync(int chordId)
    {
        // Validate input
        if (chordId <= 0)
        {
            Logger.LogWarning("Invalid chord ID: {ChordId}", chordId);
            return new Option<ChordSearchResult>.None();
        }

        Logger.LogInformation("Getting chord by ID: {ChordId}", chordId);

        var url = $"{_apiBaseUrl}/api/Chords/{chordId}";
        var tryChord = await _httpClient.GetAsync<ChordSearchResult>(url);

        return tryChord.Match<Option<ChordSearchResult>>(
            onSuccess: chord => new Option<ChordSearchResult>.Some(chord),
            onFailure: ex =>
            {
                Logger.LogWarning(ex, "Failed to get chord by ID: {ChordId}. Falling back to demo data.", chordId);
                var demoChord = GetDemoChordById(chordId);
                if (demoChord != null)
                {
                    return new Option<ChordSearchResult>.Some(demoChord);
                }

                return new Option<ChordSearchResult>.None();
            }
        );
    }

    /// <summary>
    ///     Search chords with retry logic
    /// </summary>
    public async Task<Result<List<ChordSearchResult>, SearchError>> SearchChordsWithRetryAsync(
        string query,
        int limit = 10,
        int maxRetries = 3)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(query))
        {
            return new Result<List<ChordSearchResult>, SearchError>.Failure(
                new SearchError(SearchErrorType.ValidationError, "Query cannot be empty")
            );
        }

        Logger.LogInformation("Searching chords with retry: {Query}", query);

        var url = $"{_apiBaseUrl}/api/VectorSearch/semantic?q={Uri.EscapeDataString(query)}&limit={limit}";
        var tryResults = await _httpClient.GetWithRetryAsync<List<ChordSearchResult>>(
            url,
            maxAttempts: maxRetries,
            delay: TimeSpan.FromSeconds(1)
        );

        return tryResults.Match(
            onSuccess: results => new Result<List<ChordSearchResult>, SearchError>.Success(results),
            onFailure: ex =>
            {
                Logger.LogError(ex, "All retry attempts failed for query: {Query}. Falling back to demo data.", query);
                var demoResults = GetDemoChordResults(query, limit);
                return new Result<List<ChordSearchResult>, SearchError>.Success(demoResults);
            }
        );
    }

    /// <summary>
    ///     Batch search for multiple queries
    /// </summary>
    public async Task<List<Result<List<ChordSearchResult>, SearchError>>> BatchSearchAsync(
        List<string> queries,
        int limit = 10)
    {
        var tasks = queries.Select(query => SearchChordsAsync(query, limit));
        return (await Task.WhenAll(tasks)).ToList();
    }

    // Demo data methods (same as original)
    private List<ChordSearchResult> GetDemoChordResults(string query, int limit)
    {
        var demoChords = new List<ChordSearchResult>
        {
            new()
            {
                Id = 1, Name = "Cmaj7", Quality = "Major", Extension = "7th", StackingType = "Tertian", NoteCount = 4,
                Description = "C major 7th chord", Score = 0.95, Intervals = ["P1", "M3", "P5", "M7"],
                PitchClassSet = [0, 4, 7, 11], ParentScale = "C Major"
            },
            new()
            {
                Id = 2, Name = "Dm7", Quality = "Minor", Extension = "7th", StackingType = "Tertian", NoteCount = 4,
                Description = "D minor 7th chord", Score = 0.90, Intervals = ["P1", "m3", "P5", "m7"],
                PitchClassSet = [2, 5, 9, 0], ParentScale = "C Major"
            },
            new()
            {
                Id = 3, Name = "G7", Quality = "Dominant", Extension = "7th", StackingType = "Tertian", NoteCount = 4,
                Description = "G dominant 7th chord", Score = 0.88, Intervals = ["P1", "M3", "P5", "m7"],
                PitchClassSet = [7, 11, 2, 5], ParentScale = "C Major"
            },
            new()
            {
                Id = 4, Name = "Am7", Quality = "Minor", Extension = "7th", StackingType = "Tertian", NoteCount = 4,
                Description = "A minor 7th chord", Score = 0.85, Intervals = ["P1", "m3", "P5", "m7"],
                PitchClassSet = [9, 0, 4, 7], ParentScale = "C Major"
            },
            new()
            {
                Id = 5, Name = "Fmaj7", Quality = "Major", Extension = "7th", StackingType = "Tertian", NoteCount = 4,
                Description = "F major 7th chord", Score = 0.82, Intervals = ["P1", "M3", "P5", "M7"],
                PitchClassSet = [5, 9, 0, 4], ParentScale = "F Major"
            },
            new()
            {
                Id = 6, Name = "Em7", Quality = "Minor", Extension = "7th", StackingType = "Tertian", NoteCount = 4,
                Description = "E minor 7th chord", Score = 0.80, Intervals = ["P1", "m3", "P5", "m7"],
                PitchClassSet = [4, 7, 11, 2], ParentScale = "C Major"
            },
            new()
            {
                Id = 7, Name = "C quartal", Quality = "Quartal", Extension = "Triad", StackingType = "Quartal",
                NoteCount = 3, Description = "C quartal chord built from fourths", Score = 0.75,
                Intervals = ["P1", "P4", "m7"], PitchClassSet = [0, 5, 10], ParentScale = "Various"
            },
            new()
            {
                Id = 8, Name = "Bm7b5", Quality = "Half-diminished", Extension = "7th", StackingType = "Tertian",
                NoteCount = 4, Description = "B half-diminished 7th chord", Score = 0.70,
                Intervals = ["P1", "m3", "d5", "m7"], PitchClassSet = [11, 2, 5, 9], ParentScale = "C Major"
            }
        };

        var queryLower = query.ToLowerInvariant();
        var filtered = demoChords.Where(chord =>
            chord.Name.ToLowerInvariant().Contains(queryLower) ||
            chord.Quality.ToLowerInvariant().Contains(queryLower) ||
            chord.Description.ToLowerInvariant().Contains(queryLower) ||
            (queryLower.Contains("jazz") && (chord.Extension == "7th" || chord.Quality == "Dominant")) ||
            (queryLower.Contains("major") && chord.Quality == "Major") ||
            (queryLower.Contains("minor") && chord.Quality == "Minor") ||
            (queryLower.Contains("quartal") && chord.StackingType == "Quartal")
        ).Take(limit).ToList();

        return filtered.Any() ? filtered : demoChords.Take(limit).ToList();
    }

    private List<ChordSearchResult> GetDemoSimilarChords(int chordId, int limit)
    {
        var similarChords = new List<ChordSearchResult>
        {
            new()
            {
                Id = 101, Name = "Cmaj9", Quality = "Major", Extension = "9th", StackingType = "Tertian", NoteCount = 5,
                Description = "C major 9th chord", Score = 0.92, Intervals = ["P1", "M3", "P5", "M7", "M9"],
                PitchClassSet = [0, 4, 7, 11, 2], ParentScale = "C Major"
            },
            new()
            {
                Id = 102, Name = "C6", Quality = "Major", Extension = "6th", StackingType = "Tertian", NoteCount = 4,
                Description = "C major 6th chord", Score = 0.88, Intervals = ["P1", "M3", "P5", "M6"],
                PitchClassSet = [0, 4, 7, 9], ParentScale = "C Major"
            },
            new()
            {
                Id = 103, Name = "Cadd9", Quality = "Major", Extension = "Add9", StackingType = "Tertian",
                NoteCount = 4, Description = "C add 9 chord", Score = 0.85, Intervals = ["P1", "M3", "P5", "M9"],
                PitchClassSet = [0, 4, 7, 2], ParentScale = "C Major"
            }
        };

        return similarChords.Take(limit).ToList();
    }

    private ChordSearchResult? GetDemoChordById(int chordId)
    {
        return new ChordSearchResult
        {
            Id = chordId,
            Name = "Demo Chord",
            Quality = "Major",
            Extension = "Triad",
            StackingType = "Tertian",
            NoteCount = 3,
            Description = $"Demo chord with ID {chordId} (API not available)",
            Score = 1.0,
            Intervals = ["P1", "M3", "P5"],
            PitchClassSet = [0, 4, 7],
            ParentScale = "Demo Scale"
        };
    }
}

/// <summary>
///     Search error types
/// </summary>
public enum SearchErrorType
{
    ValidationError,
    NetworkError,
    NotFound
}

/// <summary>
///     Search error details
/// </summary>
public record SearchError(SearchErrorType Type, string Message);

/// <summary>
///     Extension methods for registering monadic chord search service
/// </summary>
public static class MonadicChordSearchServiceExtensions
{
    public static IServiceCollection AddMonadicChordSearchService(this IServiceCollection services)
    {
        services.AddScoped<MonadicChordSearchService>();
        return services;
    }
}
