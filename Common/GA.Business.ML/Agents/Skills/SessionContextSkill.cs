namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;
using GA.Business.Core.Context;
using GA.Business.Core.Session;
using GA.Domain.Core.Theory.Tonal;

/// <summary>
/// Handles user preference updates via chat: skill level, genre, and key.
/// Pure domain logic — no LLM calls.
/// </summary>
public sealed class SessionContextSkill(
    ISessionContextProvider sessionContextProvider,
    ILogger<SessionContextSkill> logger) : IOrchestratorSkill
{
    public string Name        => "SessionContext";
    public string Description => "Updates user session preferences (skill level, genre, key)";

    private static readonly Regex LevelPattern = new(
        @"\b(?:i(?:'?m|(?:\s+am))\s+(?:an?\s+)?)(beginner|intermediate|advanced|expert)\b" +
        @"|\bset\s+(?:my\s+)?level\s+(?:to\s+)?(beginner|intermediate|advanced|expert)\b" +
        @"|\b(?:my\s+level\s+is\s+)(beginner|intermediate|advanced|expert)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GenrePattern = new(
        @"\bi\s+(?:play|like|love|prefer|enjoy)\s+(rock|jazz|blues|classical|metal|folk|country|funk|soul|r&b|pop|fusion)\b" +
        @"|\bset\s+(?:my\s+)?genre\s+(?:to\s+)?(rock|jazz|blues|classical|metal|folk|country|funk|soul|r&b|pop|fusion)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex KeyPattern = new(
        @"\bset\s+(?:my\s+)?key\s+(?:to\s+)?([A-G][#b]?\s*(?:major|minor|maj|min)?)\b" +
        @"|\bkey\s+of\s+([A-G][#b]?\s*(?:major|minor|maj|min)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool CanHandle(string message)
    {
        var q = message.ToLowerInvariant();
        return LevelPattern.IsMatch(message) || GenrePattern.IsMatch(message) ||
               (KeyPattern.IsMatch(message) && (q.Contains("set") || q.Contains("change") || q.Contains("switch")));
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var updates = new List<string>();

        // Parse skill level
        var levelMatch = LevelPattern.Match(message);
        if (levelMatch.Success)
        {
            var levelStr = levelMatch.Groups.Cast<Group>()
                .Skip(1).FirstOrDefault(g => g.Success)?.Value ?? string.Empty;

            if (Enum.TryParse<SkillLevel>(levelStr, ignoreCase: true, out var level))
            {
                sessionContextProvider.UpdateContext(ctx => ctx.WithSkillLevel(level));
                updates.Add($"skill level to **{level}**");
                logger.LogDebug("SessionContextSkill: set level to {Level}", level);
            }
        }

        // Parse genre
        var genreMatch = GenrePattern.Match(message);
        if (genreMatch.Success)
        {
            var genreStr = genreMatch.Groups.Cast<Group>()
                .Skip(1).FirstOrDefault(g => g.Success)?.Value ?? string.Empty;

            var genre = ParseGenre(genreStr);
            if (genre is not null)
            {
                sessionContextProvider.UpdateContext(ctx => ctx.WithGenre(genre.Value));
                updates.Add($"genre to **{genre}**");
                logger.LogDebug("SessionContextSkill: set genre to {Genre}", genre);
            }
        }

        // Parse key
        var keyMatch = KeyPattern.Match(message);
        if (keyMatch.Success)
        {
            var keyStr = keyMatch.Groups.Cast<Group>()
                .Skip(1).FirstOrDefault(g => g.Success)?.Value?.Trim() ?? string.Empty;

            var key = TryParseKey(keyStr);
            if (key is not null)
            {
                sessionContextProvider.UpdateContext(ctx => ctx.WithKey(key));
                updates.Add($"key to **{key}**");
                logger.LogDebug("SessionContextSkill: set key to {Key}", key);
            }
        }

        if (updates.Count == 0)
            return Task.FromResult(new AgentResponse
            {
                AgentId    = AgentIds.Theory,
                Result     = "I couldn't understand the preference you wanted to set. " +
                             "Try something like \"I'm a beginner\", \"I play jazz\", or \"set key to C major\".",
                Confidence = 0.3f,
                Evidence   = [],
                Assumptions = ["Could not parse preference update"]
            });

        var confirmation = updates.Count == 1
            ? $"Got it! I've updated your {updates[0]}. I'll adapt my responses accordingly."
            : $"Got it! I've updated your {string.Join(" and ", updates)}. I'll adapt my responses accordingly.";

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = confirmation,
            Confidence = 1.0f,
            Evidence   = updates.Select(u => $"Updated {u}").ToList(),
            Assumptions = []
        });
    }

    private static MusicalGenre? ParseGenre(string genre) => genre.ToLowerInvariant() switch
    {
        "rock"      => MusicalGenre.Rock,
        "jazz"      => MusicalGenre.Jazz,
        "blues"     => MusicalGenre.Blues,
        "classical" => MusicalGenre.Classical,
        "metal"     => MusicalGenre.Metal,
        "folk"      => MusicalGenre.Folk,
        "country"   => MusicalGenre.Country,
        "funk"      => MusicalGenre.Funk,
        "soul"      => MusicalGenre.Soul,
        "r&b"       => MusicalGenre.RAndB,
        "pop"       => MusicalGenre.Pop,
        "fusion"    => MusicalGenre.Fusion,
        _           => null
    };

    private static Key? TryParseKey(string keyStr)
    {
        var match = Regex.Match(keyStr, @"([A-G][#b]?)\s*(major|minor|maj|min)?", RegexOptions.IgnoreCase);
        if (!match.Success) return null;

        var rootStr = match.Groups[1].Value;
        var modeStr = match.Groups[2].Value.ToLowerInvariant();
        var isMinor = modeStr is "minor" or "min";

        return Key.Items.FirstOrDefault(k =>
            k.KeyMode == (isMinor ? KeyMode.Minor : KeyMode.Major) &&
            string.Equals(k.Root.ToString(), rootStr, StringComparison.OrdinalIgnoreCase));
    }
}
