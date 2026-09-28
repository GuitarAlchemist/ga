namespace GA.Business.ML.Agents.Skills;

using System.Text.RegularExpressions;

/// <summary>
/// Detects an improvisation request that names only invalid chords ("which arpeggio
/// fits Hm Q7") and builds the answer that declines it (ga#745). Without this, such a
/// request scores below every intent, reaches the LLM fallback, and gets invented
/// theory about the invalid tokens ("The note Hm Q7 (also known as H) is a perfect
/// 4th above the note Q").
/// </summary>
/// <remarks>
/// <para>
/// A token is an invalid chord name when it is shaped like a chord symbol (an
/// uppercase letter, an optional accidental, then a chord suffix) but its root letter
/// is outside A–G. A capital with no suffix ("I", "X") is never one, and a bare "M"
/// counts as a quality only before a digit ("CM7"), so "PM" is not one either.
/// </para>
/// <para>
/// The check fires only when the request has <see cref="ImprovisationSkill"/>'s
/// intent and names no valid chord, so a request that mixes valid and invalid
/// chords keeps its current route. An interjection opening the message ("Hm, which
/// mode is brightest?") is not read as a chord.
/// </para>
/// </remarks>
public static partial class InvalidChordNames
{
    /// <summary>Routing method recorded when the orchestrator declines through this check.</summary>
    public const string RoutingMethod = "invalid-chord-guard";

    /// <summary>Routing confidence of the decline: the input is known to name no valid chord.</summary>
    public const float DeclineConfidence = 0.9f;

    /// <summary>
    /// The invalid chord names in <paramref name="message"/>, in order, or an empty list
    /// when the message is not an improvisation request, names a valid chord, or has no
    /// invalid one.
    /// </summary>
    public static IReadOnlyList<string> Find(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return [];
        if (!ImprovisationSkill.HasImprovIntent(message)) return [];
        if (ImprovisationSkill.NamesValidChord(message)) return [];

        var tokens = new List<string>();
        foreach (Match m in InvalidRootChordRegex().Matches(message))
        {
            // A bare capital ("I", "X" in "X-ray") has no chord suffix: not a chord name.
            if (m.Groups["suffix"].Length == 0) continue;
            if (IsOpeningInterjection(message, m)) continue;
            if (!tokens.Contains(m.Value)) tokens.Add(m.Value);
        }
        return tokens;
    }

    /// <summary>The decline answer for the invalid chord names found by <see cref="Find"/>.</summary>
    public static string Decline(IReadOnlyList<string> tokens)
    {
        var quoted = tokens.Select(t => $"\"{t}\"").ToList();
        var list = quoted.Count == 1
            ? quoted[0]
            : $"{string.Join(", ", quoted.Take(quoted.Count - 1))} and {quoted[^1]}";
        var noun = tokens.Count == 1 ? "a chord name" : "chord names";

        var answer =
            $"I don't recognize {list} as {noun}, so I can't suggest arpeggios or scales for " +
            (tokens.Count == 1 ? "it" : "them") + ". A chord name starts with a root letter " +
            "from A to G, optionally followed by # or b, then the chord quality: for example " +
            "'Bm', 'G7' or 'Cmaj7'.";

        // German notation writes B natural as H, so "Hm" is a real chord there.
        var german = tokens.FirstOrDefault(t => t[0] == 'H');
        if (german is not null)
            answer += $" In German notation H is B: \"{german}\" is written \"B{german[1..]}\" here.";

        return answer + " Name the chords again and I'll give the arpeggio and scales for each.";
    }

    private static bool IsOpeningInterjection(string message, Match match) =>
        message[..match.Index].Trim().Length == 0
        && match.Index + match.Length < message.Length
        && message[match.Index + match.Length] is ',' or '.' or '!' or '?' or '…';

    // Uppercase root outside A-G, optional accidental, then a chord suffix: a quality,
    // an extension, alterations. Find skips an empty suffix. Whole token only: "Hmm" and
    // "Madden" fail the trailing guard. Case-sensitive, like the skill's chord regexes.
    [GeneratedRegex(@"(?<![\w#])(?<root>[H-Z][#b]?)(?<suffix>(?:maj|min|dim|aug|sus|add|m|M(?=\d)|°|ø|Δ|\+|-)?\d{0,2}(?:(?:b|#|\+|alt|sus|add)\d{0,2})*)(?![\w#])")]
    private static partial Regex InvalidRootChordRegex();
}
