namespace GuitarAlchemistChatbot.Services;

using System.Net;
using System.Text.Json;

public class ChordSearchService
{
    private readonly string _apiBaseUrl;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ChordSearchService> _logger;

    public ChordSearchService(
        HttpClient httpClient,
        ILogger<ChordSearchService> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _apiBaseUrl = _configuration["GuitarAlchemistApi:BaseUrl"] ?? "http://localhost:5000";
    }


    /// <summary>
    ///     Search for chords using natural language queries
    /// </summary>
    public async Task<List<ChordSearchResult>> SearchChordsAsync(
        string query,
        int limit = 10,
        int numCandidates = 100)
    {
        try
        {
            _logger.LogInformation("Searching chords with query: {Query}", query);

            var url =
                $"{_apiBaseUrl}/api/VectorSearch/semantic?q={Uri.EscapeDataString(query)}&limit={limit}&numCandidates={numCandidates}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<List<ChordSearchResult>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return results ?? [];
            }

            _logger.LogWarning("API call failed with status: {StatusCode}. Falling back to demo data.",
                response.StatusCode);
            return GetDemoChordResults(query, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching chords with query: {Query}. Falling back to demo data.", query);
            return GetDemoChordResults(query, limit);
        }
    }

    /// <summary>
    ///     Find chords similar to a given chord
    /// </summary>
    public async Task<List<ChordSearchResult>> FindSimilarChordsAsync(
        int chordId,
        int limit = 10,
        int numCandidates = 100)
    {
        try
        {
            _logger.LogInformation("Finding similar chords for chord ID: {ChordId}", chordId);

            var url = $"{_apiBaseUrl}/api/VectorSearch/similar/{chordId}?limit={limit}&numCandidates={numCandidates}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var results = JsonSerializer.Deserialize<List<ChordSearchResult>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return results ?? [];
            }

            _logger.LogWarning(
                "API call failed with status: {StatusCode} for chord ID: {ChordId}. Falling back to demo data.",
                response.StatusCode, chordId);
            return GetDemoSimilarChords(chordId, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding similar chords for chord ID: {ChordId}. Falling back to demo data.",
                chordId);
            return GetDemoSimilarChords(chordId, limit);
        }
    }

    /// <summary>
    ///     Get chord details by ID
    /// </summary>
    public async Task<ChordSearchResult?> GetChordByIdAsync(int chordId)
    {
        try
        {
            var url = $"{_apiBaseUrl}/api/Chords/{chordId}";
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var chord = JsonSerializer.Deserialize<ChordSearchResult>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return chord;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            _logger.LogWarning(
                "API call failed with status: {StatusCode} for chord ID: {ChordId}. Falling back to demo data.",
                response.StatusCode, chordId);
            return GetDemoChordById(chordId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting chord by ID: {ChordId}. Falling back to demo data.", chordId);
            return GetDemoChordById(chordId);
        }
    }

    /// <summary>
    ///     Provides demo chord results when API is not available
    /// </summary>
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

        // Filter based on query keywords
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

    /// <summary>
    ///     Provides demo similar chords when API is not available
    /// </summary>
    private List<ChordSearchResult> GetDemoSimilarChords(int chordId, int limit)
    {
        // Return some related chords based on the chord ID
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

    /// <summary>
    ///     Provides demo chord details when API is not available
    /// </summary>
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

public class ChordSearchResult
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Quality { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string StackingType { get; set; } = string.Empty;
    public int NoteCount { get; set; }
    public string Description { get; set; } = string.Empty;
    public double Score { get; set; }
    public List<string> Intervals { get; set; } = [];
    public List<int> PitchClassSet { get; set; } = [];
    public string ParentScale { get; set; } = string.Empty;
}
