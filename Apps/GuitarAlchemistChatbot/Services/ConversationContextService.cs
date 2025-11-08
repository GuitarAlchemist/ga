namespace GuitarAlchemistChatbot.Services;

/// <summary>
///     Service for tracking conversation context and short-term memory
/// </summary>
public class ConversationContextService(ILogger<ConversationContextService> logger)
{
    private readonly List<ContextEntry> _contextHistory = [];
    private readonly Dictionary<string, object> _namedReferences = new();

    /// <summary>
    ///     Add a user query to the context
    /// </summary>
    public void AddUserQuery(string query)
    {
        _contextHistory.Add(new ContextEntry
        {
            Timestamp = DateTime.UtcNow,
            Type = ContextType.UserQuery,
            Content = query
        });

        // Keep only last 20 entries
        if (_contextHistory.Count > 20)
        {
            _contextHistory.RemoveAt(0);
        }

        logger.LogInformation("Added user query to context: {Query}", query);
    }

    /// <summary>
    ///     Add a chord reference to the context
    /// </summary>
    public void AddChordReference(int chordId, string chordName)
    {
        _contextHistory.Add(new ContextEntry
        {
            Timestamp = DateTime.UtcNow,
            Type = ContextType.ChordReference,
            Content = chordName,
            ReferenceId = chordId
        });

        // Store as named reference
        _namedReferences["last_chord"] = new ChordReference { Id = chordId, Name = chordName };
        _namedReferences["the_chord"] = new ChordReference { Id = chordId, Name = chordName };

        logger.LogInformation("Added chord reference to context: {ChordName} (ID: {ChordId})", chordName, chordId);
    }

    /// <summary>
    ///     Add a scale reference to the context
    /// </summary>
    public void AddScaleReference(string scaleName)
    {
        _contextHistory.Add(new ContextEntry
        {
            Timestamp = DateTime.UtcNow,
            Type = ContextType.ScaleReference,
            Content = scaleName
        });

        _namedReferences["last_scale"] = scaleName;
        _namedReferences["the_scale"] = scaleName;

        logger.LogInformation("Added scale reference to context: {ScaleName}", scaleName);
    }

    /// <summary>
    ///     Add a music theory concept to the context
    /// </summary>
    public void AddConceptReference(string concept)
    {
        _contextHistory.Add(new ContextEntry
        {
            Timestamp = DateTime.UtcNow,
            Type = ContextType.ConceptReference,
            Content = concept
        });

        _namedReferences["last_concept"] = concept;

        logger.LogInformation("Added concept reference to context: {Concept}", concept);
    }

    /// <summary>
    ///     Add a user preference to the context
    /// </summary>
    public void AddPreference(string key, string value)
    {
        _contextHistory.Add(new ContextEntry
        {
            Timestamp = DateTime.UtcNow,
            Type = ContextType.Preference,
            Content = $"{key}: {value}"
        });

        _namedReferences[$"pref_{key}"] = value;

        logger.LogInformation("Added preference to context: {Key} = {Value}", key, value);
    }

    /// <summary>
    ///     Get the last referenced chord
    /// </summary>
    public ChordReference? GetLastChord()
    {
        if (_namedReferences.TryGetValue("last_chord", out var chord) && chord is ChordReference chordRef)
        {
            return chordRef;
        }

        return null;
    }

    /// <summary>
    ///     Get the last referenced scale
    /// </summary>
    public string? GetLastScale()
    {
        if (_namedReferences.TryGetValue("last_scale", out var scale) && scale is string scaleName)
        {
            return scaleName;
        }

        return null;
    }

    /// <summary>
    ///     Get recent context entries
    /// </summary>
    public List<ContextEntry> GetRecentContext(int count = 10)
    {
        return _contextHistory.TakeLast(count).ToList();
    }

    /// <summary>
    ///     Get context summary for display
    /// </summary>
    public string GetContextSummary()
    {
        var summary = new List<string>();

        var lastChord = GetLastChord();
        if (lastChord != null)
        {
            summary.Add($"Last chord: {lastChord.Name}");
        }

        var lastScale = GetLastScale();
        if (lastScale != null)
        {
            summary.Add($"Last scale: {lastScale}");
        }

        var recentQueries = _contextHistory
            .Where(e => e.Type == ContextType.UserQuery)
            .TakeLast(3)
            .Select(e => e.Content)
            .ToList();

        if (recentQueries.Any())
        {
            summary.Add(
                $"Recent topics: {string.Join(", ", recentQueries.Select(q => q.Length > 30 ? q.Substring(0, 30) + "..." : q))}");
        }

        return summary.Any() ? string.Join(" | ", summary) : "No context yet";
    }

    /// <summary>
    ///     Clear all context
    /// </summary>
    public void Clear()
    {
        _contextHistory.Clear();
        _namedReferences.Clear();
        logger.LogInformation("Cleared conversation context");
    }

    /// <summary>
    ///     Get context for AI prompt enhancement
    /// </summary>
    public string GetContextForPrompt()
    {
        var contextParts = new List<string>();

        var lastChord = GetLastChord();
        if (lastChord != null)
        {
            contextParts.Add($"The user recently discussed the chord: {lastChord.Name} (ID: {lastChord.Id})");
        }

        var lastScale = GetLastScale();
        if (lastScale != null)
        {
            contextParts.Add($"The user recently discussed the scale: {lastScale}");
        }

        var recentConcepts = _contextHistory
            .Where(e => e.Type == ContextType.ConceptReference)
            .TakeLast(3)
            .Select(e => e.Content)
            .ToList();

        if (recentConcepts.Any())
        {
            contextParts.Add($"Recent concepts discussed: {string.Join(", ", recentConcepts)}");
        }

        return contextParts.Any()
            ? $"\n\nConversation Context:\n{string.Join("\n", contextParts)}"
            : "";
    }
}

/// <summary>
///     Context entry type
/// </summary>
public enum ContextType
{
    UserQuery,
    ChordReference,
    ScaleReference,
    ConceptReference,
    Preference,
    FunctionCall
}

/// <summary>
///     Context entry
/// </summary>
public class ContextEntry
{
    public DateTime Timestamp { get; set; }
    public ContextType Type { get; set; }
    public string Content { get; set; } = "";
    public int? ReferenceId { get; set; }
}

/// <summary>
///     Chord reference
/// </summary>
public class ChordReference
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
