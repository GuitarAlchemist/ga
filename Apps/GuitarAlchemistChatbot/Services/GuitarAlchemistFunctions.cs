namespace GuitarAlchemistChatbot.Services;

using System.Text;
using GA.Business.Web.Services;

/// <summary>
///     AI function tools for Guitar Alchemist chatbot
/// </summary>
public class GuitarAlchemistFunctions(
    ChordSearchService chordSearchService,
    ConversationContextService contextService,
    WebSearchService webSearchService,
    WebScrapingService webScrapingService,
    FeedReaderService feedReaderService,
    ILogger<GuitarAlchemistFunctions> logger)
{
    /// <summary>
    ///     Search for chords using natural language descriptions
    /// </summary>
    /// <param name="query">
    ///     Natural language description of the chord you're looking for (e.g., "dark jazz chords", "bright
    ///     major chords", "complex extended chords")
    /// </param>
    /// <param name="maxResults">Maximum number of results to return (default: 8)</param>
    /// <returns>List of matching chords with their details</returns>
    [Description(
        "Search for chords using natural language descriptions like 'dark jazz chords', 'bright major chords', or 'complex extended chords'")]
    public async Task<List<ChordSearchResult>> SearchChords(
        [Description("Natural language description of the chord you're looking for")]
        string query,
        [Description("Maximum number of results to return")]
        int maxResults = 8)
    {
        try
        {
            logger.LogInformation("AI function called: SearchChords with query: {Query}", query);
            contextService.AddUserQuery(query);
            var results = await chordSearchService.SearchChordsAsync(query, maxResults);
            logger.LogInformation("Found {Count} chords for query: {Query}", results.Count, query);

            // Add first result to context
            if (results.Count > 0)
            {
                contextService.AddChordReference(results[0].Id, results[0].Name);
            }

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SearchChords function");
            throw;
        }
    }

    /// <summary>
    ///     Find chords that are similar to a specific chord
    /// </summary>
    /// <param name="chordId">The ID of the chord to find similar chords for</param>
    /// <param name="maxResults">Maximum number of similar chords to return (default: 6)</param>
    /// <returns>List of similar chords</returns>
    [Description("Find chords that are similar to a specific chord by its ID")]
    public async Task<List<ChordSearchResult>> FindSimilarChords(
        [Description("The ID of the chord to find similar chords for")]
        int chordId,
        [Description("Maximum number of similar chords to return")]
        int maxResults = 6)
    {
        try
        {
            logger.LogInformation("AI function called: FindSimilarChords for chord ID: {ChordId}", chordId);
            var results = await chordSearchService.FindSimilarChordsAsync(chordId, maxResults);
            logger.LogInformation("Found {Count} similar chords for chord ID: {ChordId}", results.Count, chordId);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in FindSimilarChords function");
            throw;
        }
    }

    /// <summary>
    ///     Get detailed information about a specific chord
    /// </summary>
    /// <param name="chordId">The ID of the chord to get details for</param>
    /// <returns>Detailed chord information</returns>
    [Description("Get detailed information about a specific chord by its ID")]
    public async Task<ChordSearchResult?> GetChordDetails(
        [Description("The ID of the chord to get details for")]
        int chordId)
    {
        try
        {
            logger.LogInformation("AI function called: GetChordDetails for chord ID: {ChordId}", chordId);
            var result = await chordSearchService.GetChordByIdAsync(chordId);
            if (result != null)
            {
                logger.LogInformation("Found chord details for ID: {ChordId}, Name: {Name}", chordId, result.Name);
                contextService.AddChordReference(chordId, result.Name);
            }
            else
            {
                logger.LogWarning("No chord found for ID: {ChordId}", chordId);
            }

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetChordDetails function");
            throw;
        }
    }

    /// <summary>
    ///     Explain music theory concepts related to chords, scales, and guitar playing
    /// </summary>
    /// <param name="concept">The music theory concept to explain (e.g., "circle of fifths", "chord progressions", "modes")</param>
    /// <returns>Educational explanation of the concept</returns>
    [Description("Explain music theory concepts related to chords, scales, and guitar playing")]
    public string ExplainMusicTheory(
        [Description("The music theory concept to explain")]
        string concept)
    {
        try
        {
            logger.LogInformation("AI function called: ExplainMusicTheory for concept: {Concept}", concept);

            // This function provides structured information that the AI can use to give better explanations
            var lowerConcept = concept.ToLowerInvariant();

            if (lowerConcept.Contains("circle of fifths") || lowerConcept.Contains("circle of 5ths"))
            {
                return
                    "The Circle of Fifths is a visual representation of key signatures and their relationships. Moving clockwise adds sharps, moving counterclockwise adds flats. It helps understand chord progressions, key modulations, and relative major/minor relationships.";
            }

            if (lowerConcept.Contains("chord progression"))
            {
                return
                    "Chord progressions are sequences of chords that create harmonic movement in music. Common progressions include I-V-vi-IV (very popular in pop), ii-V-I (jazz standard), and I-vi-ii-V (circle progression). Roman numerals indicate the chord's position in the scale.";
            }

            if (lowerConcept.Contains("mode") || lowerConcept.Contains("modal"))
            {
                return
                    "Modes are variations of the major scale starting from different degrees. The seven modes are: Ionian (major), Dorian, Phrygian, Lydian, Mixolydian, Aeolian (natural minor), and Locrian. Each has a unique character and emotional quality.";
            }

            if (lowerConcept.Contains("voice leading") || lowerConcept.Contains("voicing"))
            {
                return
                    "Voice leading is the smooth movement of individual voices (notes) between chords. Good voice leading minimizes large jumps and creates smooth melodic lines. Chord voicings refer to how the notes of a chord are arranged and distributed across different octaves.";
            }

            if (lowerConcept.Contains("tension") || lowerConcept.Contains("extension"))
            {
                return
                    "Chord tensions (extensions) are notes added beyond the basic triad. Common tensions include 9ths, 11ths, and 13ths. They add color and sophistication to chords. Available tensions depend on the chord's function and the underlying scale.";
            }

            return
                $"I can help explain various music theory concepts. For '{concept}', I'd recommend asking more specifically about aspects like chord construction, scale relationships, or practical guitar applications.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ExplainMusicTheory function");
            return
                "I encountered an error while explaining that concept. Please try asking about it in a different way.";
        }
    }

    /// <summary>
    ///     Search Wikipedia for music theory information
    /// </summary>
    /// <param name="query">Search query for Wikipedia (e.g., "harmonic minor scale", "jazz chord progressions")</param>
    /// <param name="maxResults">Maximum number of results to return (default: 3)</param>
    /// <returns>Wikipedia search results with summaries</returns>
    [Description("Search Wikipedia for music theory information and concepts")]
    public async Task<string> SearchWikipedia(
        [Description("Search query for Wikipedia")]
        string query,
        [Description("Maximum number of results to return")]
        int maxResults = 3)
    {
        try
        {
            logger.LogInformation("AI function called: SearchWikipedia with query: {Query}", query);
            contextService.AddConceptReference(query);
            var results = await webSearchService.SearchWikipediaAsync(query, maxResults);
            logger.LogInformation("Found Wikipedia results for query: {Query}", query);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SearchWikipedia function");
            return $"I encountered an error searching Wikipedia: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get a detailed Wikipedia summary for a specific music theory topic
    /// </summary>
    /// <param name="title">Wikipedia article title (e.g., "Circle of fifths", "Dorian mode")</param>
    /// <returns>Detailed Wikipedia article summary</returns>
    [Description("Get a detailed Wikipedia summary for a specific music theory topic")]
    public async Task<string> GetWikipediaSummary(
        [Description("Wikipedia article title")]
        string title)
    {
        try
        {
            logger.LogInformation("AI function called: GetWikipediaSummary for title: {Title}", title);
            contextService.AddConceptReference(title);
            var summary = await webSearchService.GetWikipediaSummaryAsync(title);
            logger.LogInformation("Retrieved Wikipedia summary for: {Title}", title);
            return summary;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetWikipediaSummary function");
            return $"I encountered an error retrieving the Wikipedia summary: {ex.Message}";
        }
    }

    /// <summary>
    ///     Search music theory websites for specific information
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="site">Website to search (musictheory, justinguitar, guitarnoise, teoria)</param>
    /// <returns>Search results from the specified music theory website</returns>
    [Description("Search specific music theory websites for guitar lessons and theory information")]
    public async Task<string> SearchMusicTheorySite(
        [Description("Search query")] string query,
        [Description("Website to search: musictheory, justinguitar, guitarnoise, or teoria")]
        string site = "musictheory")
    {
        try
        {
            logger.LogInformation("AI function called: SearchMusicTheorySite with query: {Query}, site: {Site}", query,
                site);
            contextService.AddConceptReference(query);
            var results = await webSearchService.SearchMusicTheorySitesAsync(query, site);
            logger.LogInformation("Found results from {Site} for query: {Query}", site, query);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SearchMusicTheorySite function");
            return $"I encountered an error searching {site}: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get the latest music lessons and articles from RSS feeds
    /// </summary>
    /// <param name="source">Feed source (musictheory, justinguitar, guitarnoise, teoria)</param>
    /// <param name="maxItems">Maximum number of items to return (default: 5)</param>
    /// <returns>Latest music lessons and articles</returns>
    [Description("Get the latest music lessons and articles from popular guitar and music theory websites")]
    public async Task<string> GetLatestMusicLessons(
        [Description("Feed source: musictheory, justinguitar, guitarnoise, or teoria")]
        string source = "justinguitar",
        [Description("Maximum number of items to return")]
        int maxItems = 5)
    {
        try
        {
            logger.LogInformation("AI function called: GetLatestMusicLessons from source: {Source}", source);
            var results = await feedReaderService.ReadFeedAsync(source, maxItems);
            logger.LogInformation("Retrieved {Count} items from {Source}", maxItems, source);
            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetLatestMusicLessons function");
            return $"I encountered an error retrieving lessons from {source}: {ex.Message}";
        }
    }

    /// <summary>
    ///     Fetch and extract content from a music theory article or webpage
    /// </summary>
    /// <param name="url">URL of the music theory article</param>
    /// <returns>Extracted article content</returns>
    [Description("Fetch and extract content from a music theory article or webpage")]
    public async Task<string> FetchMusicTheoryArticle(
        [Description("URL of the music theory article")]
        string url)
    {
        try
        {
            logger.LogInformation("AI function called: FetchMusicTheoryArticle for URL: {Url}", url);
            var content = await webScrapingService.FetchWebPageAsync(url, true);
            logger.LogInformation("Fetched content from URL: {Url}", url);
            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in FetchMusicTheoryArticle function");
            return $"I encountered an error fetching the article: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get common chord progression templates by genre
    /// </summary>
    /// <param name="genre">Genre filter (pop, jazz, blues, rock, etc.) or 'all' for all genres</param>
    /// <returns>Formatted list of chord progression templates</returns>
    [Description("Get common chord progression templates organized by genre (pop, jazz, blues, rock, etc.)")]
    public async Task<string> GetProgressionTemplates(
        [Description("Genre filter (pop, jazz, blues, rock, classical, etc.) or 'all' for all genres")]
        string genre = "all")
    {
        try
        {
            logger.LogInformation("AI function called: GetProgressionTemplates with genre: {Genre}", genre);

            var templates = genre.ToLower() == "all"
                ? ChordProgressionTemplates.GetAllTemplates().Values
                : ChordProgressionTemplates.GetByGenre(genre);

            if (!templates.Any())
            {
                return
                    $"I couldn't find any progression templates for the genre '{genre}'. Try 'pop', 'jazz', 'blues', 'rock', or 'all'.";
            }

            var result = new StringBuilder();
            result.AppendLine($"## Chord Progression Templates{(genre.ToLower() != "all" ? $" - {genre}" : "")}\n");

            foreach (var template in templates)
            {
                result.AppendLine(template.ToMarkdown());
                result.AppendLine();
            }

            result.AppendLine("---");
            result.AppendLine(
                "*Tip: Try these progressions in different keys and experiment with rhythm and voicings!*");

            return await Task.FromResult(result.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetProgressionTemplates function");
            return $"I encountered an error getting progression templates: {ex.Message}";
        }
    }

    /// <summary>
    ///     Search for chord progression templates by name, description, or mood
    /// </summary>
    /// <param name="query">Search query (e.g., 'sad', 'uplifting', 'blues', 'jazz')</param>
    /// <returns>Matching progression templates</returns>
    [Description("Search for chord progression templates by name, description, or mood")]
    public async Task<string> SearchProgressionTemplates(
        [Description("Search query for progression templates (e.g., 'sad', 'uplifting', 'dramatic')")]
        string query)
    {
        try
        {
            logger.LogInformation("AI function called: SearchProgressionTemplates with query: {Query}", query);
            contextService.AddUserQuery(query);

            var templates = ChordProgressionTemplates.Search(query);

            if (!templates.Any())
            {
                return
                    $"I couldn't find any progression templates matching '{query}'. Try searching for moods like 'sad', 'happy', 'dramatic', or genres like 'jazz', 'blues', 'rock'.";
            }

            var result = new StringBuilder();
            result.AppendLine($"## Progression Templates matching '{query}'\n");

            foreach (var template in templates.Take(5)) // Limit to top 5 results
            {
                result.AppendLine(template.ToMarkdown());
                result.AppendLine();
            }

            if (templates.Count() > 5)
            {
                result.AppendLine(
                    $"*...and {templates.Count() - 5} more. Try a more specific search to narrow results.*");
            }

            return await Task.FromResult(result.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SearchProgressionTemplates function");
            return $"I encountered an error searching progression templates: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get a list of all available genres for progression templates
    /// </summary>
    /// <returns>List of available genres</returns>
    [Description("Get a list of all available genres for chord progression templates")]
    public async Task<string> GetProgressionGenres()
    {
        try
        {
            logger.LogInformation("AI function called: GetProgressionGenres");

            var genres = ChordProgressionTemplates.GetGenres();
            var result = new StringBuilder();

            result.AppendLine("## Available Progression Genres\n");
            result.AppendLine("I have chord progression templates for these genres:\n");

            foreach (var genreItem in genres)
            {
                var count = ChordProgressionTemplates.GetByGenre(genreItem).Count();
                result.AppendLine($"- **{genreItem}** ({count} progression{(count != 1 ? "s" : "")})");
            }

            result.AppendLine("\n*Ask me for progressions in any of these genres, or search by mood!*");

            return await Task.FromResult(result.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetProgressionGenres function");
            return $"I encountered an error getting progression genres: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get chord diagram with finger positions
    /// </summary>
    /// <param name="chordName">Chord name (e.g., 'C', 'Dm7', 'Cmaj7')</param>
    /// <param name="position">Optional position preference ('open', 'barre', or specific fret)</param>
    /// <returns>Chord diagram information</returns>
    [Description("Get visual chord diagram showing finger positions on guitar fretboard")]
    public async Task<string> GetChordDiagram(
        [Description("Chord name (e.g., 'C', 'Dm7', 'Cmaj7', 'G7')")]
        string chordName,
        [Description("Optional position preference: 'open', 'barre', or 'all' for all positions")]
        string position = "all")
    {
        try
        {
            logger.LogInformation("AI function called: GetChordDiagram for chord: {ChordName}, position: {Position}",
                chordName, position);

            var voicings = ChordVoicingLibrary.GetVoicings(chordName);

            if (voicings == null || !voicings.Any())
            {
                return
                    $"I don't have a chord diagram for '{chordName}' yet. Try common chords like C, D, Em, G7, Cmaj7, etc.";
            }

            var result = new StringBuilder();
            result.AppendLine($"## {chordName} Chord Diagram{(voicings.Count > 1 ? "s" : "")}\n");

            // Filter by position if specified
            var filteredVoicings = position.ToLower() switch
            {
                "open" => voicings.Where(v => v.Position.Contains("Open", StringComparison.OrdinalIgnoreCase)).ToList(),
                "barre" => voicings.Where(v =>
                    v.Position.Contains("Barre", StringComparison.OrdinalIgnoreCase) || v.Barre != null).ToList(),
                _ => voicings
            };

            if (!filteredVoicings.Any())
            {
                filteredVoicings = voicings; // Fall back to all if filter returns nothing
            }

            foreach (var voicing in filteredVoicings)
            {
                result.AppendLine($"### {voicing.GetDescription()}");
                result.AppendLine($"**Notes:** {voicing.Notes}");
                result.AppendLine();

                // Format as a special marker that the UI can render as a diagram
                result.AppendLine("```chord-diagram");
                result.AppendLine($"name: {voicing.FullName}");
                result.AppendLine($"frets: {string.Join(",", voicing.Frets)}");
                result.AppendLine($"fingers: {string.Join(",", voicing.Fingers)}");
                result.AppendLine($"notes: {voicing.Notes}");
                result.AppendLine($"startFret: {voicing.StartFret}");
                if (voicing.Barre != null)
                {
                    result.AppendLine(
                        $"barre: {voicing.Barre.Fret},{voicing.Barre.FromString},{voicing.Barre.ToString}");
                }

                result.AppendLine("```");
                result.AppendLine();
            }

            result.AppendLine("*Tip: Numbers indicate which finger to use (1=index, 2=middle, 3=ring, 4=pinky)*");

            return await Task.FromResult(result.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in GetChordDiagram function");
            return $"I encountered an error getting the chord diagram: {ex.Message}";
        }
    }

    /// <summary>
    ///     List all available chords with diagrams
    /// </summary>
    /// <returns>List of available chords</returns>
    [Description("List all chords that have visual diagrams available")]
    public async Task<string> ListAvailableChordDiagrams()
    {
        try
        {
            logger.LogInformation("AI function called: ListAvailableChordDiagrams");

            var chordNames = ChordVoicingLibrary.GetAllChordNames();
            var result = new StringBuilder();

            result.AppendLine("## Available Chord Diagrams\n");
            result.AppendLine("I have visual chord diagrams for these chords:\n");

            // Group by type
            var major = chordNames
                .Where(c => !c.Contains("m") && !c.Contains("7") && !c.Contains("dim") && !c.Contains("aug")).ToList();
            var minor = chordNames.Where(c => c.Contains("m") && !c.Contains("7")).ToList();
            var seventh = chordNames.Where(c => c.Contains("7")).ToList();
            var extended = chordNames.Where(c => c.Contains("9")).ToList();
            var altered = chordNames.Where(c => c.Contains("dim") || c.Contains("aug")).ToList();

            if (major.Any())
            {
                result.AppendLine("**Major Chords:**");
                result.AppendLine(string.Join(", ", major));
                result.AppendLine();
            }

            if (minor.Any())
            {
                result.AppendLine("**Minor Chords:**");
                result.AppendLine(string.Join(", ", minor));
                result.AppendLine();
            }

            if (seventh.Any())
            {
                result.AppendLine("**Seventh Chords:**");
                result.AppendLine(string.Join(", ", seventh));
                result.AppendLine();
            }

            if (extended.Any())
            {
                result.AppendLine("**Extended Chords:**");
                result.AppendLine(string.Join(", ", extended));
                result.AppendLine();
            }

            if (altered.Any())
            {
                result.AppendLine("**Altered Chords:**");
                result.AppendLine(string.Join(", ", altered));
                result.AppendLine();
            }

            result.AppendLine("*Ask me to show you a diagram for any of these chords!*");

            return await Task.FromResult(result.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ListAvailableChordDiagrams function");
            return $"I encountered an error listing chord diagrams: {ex.Message}";
        }
    }
}
