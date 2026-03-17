namespace GA.Business.ML.Agents.Skills;

using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;

/// <summary>
/// Answers queries about chord structures and qualities (e.g., "what is a major seventh chord?" or "notes in Cmaj7?")
/// using pure domain computation — zero LLM calls.
/// </summary>
/// <remarks>
/// Supports multiple modes:
/// <list type="bullet">
/// <item><b>Chord quality explained</b>: "What is a dominant 7th?" — explains quality with intervals and common uses.</item>
/// <item><b>Specific chord</b>: "What notes in Cmaj7?" or "Notes in F#m7b5?" — lists the notes and intervals.</item>
/// <item><b>Chord voicing info</b>: "How do I play Gm?" — suggests standard voicings.</item>
/// </list>
/// </remarks>
public sealed class ChordExplanationSkill(ILogger<ChordExplanationSkill> logger) : IOrchestratorSkill
{
    public string Name        => "ChordExplanation";
    public string Description => "Explains chord qualities, notes, and voicings for any chord";

    // ── Patterns ──────────────────────────────────────────────────────────────

    private static readonly Regex ChordQualityPattern = new(
        @"\b(major|minor|dominant|diminished|augmented|suspended|major\s+seventh|minor\s+seventh|half.diminished)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SpecificChordPattern = new(
        @"\b([A-G][#b]?)(?:maj7|maj|min7|min|m7|m7b5|m|dim7|dim|aug|7|sus2|sus4)?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── Root note mapping ─────────────────────────────────────────────────────

    private static readonly FrozenDictionary<string, int> RootPcMap =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["C"] = 0,  ["C#"] = 1, ["Db"] = 1,
            ["D"] = 2,               ["D#"] = 3, ["Eb"] = 3,
            ["E"] = 4,  ["Fb"] = 4,  ["F"] = 5,  ["E#"] = 5,
            ["F#"] = 6, ["Gb"] = 6,  ["G"] = 7,
            ["G#"] = 8, ["Ab"] = 8,  ["A"] = 9,
            ["A#"] = 10, ["Bb"] = 10, ["B"] = 11, ["Cb"] = 11,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly string[] RootNames =
        ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    // ── Chord quality data ────────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<string, ChordQualityData> ChordQualities =
        new Dictionary<string, ChordQualityData>(StringComparer.OrdinalIgnoreCase)
        {
            ["Major"]              = new([0, 4, 7],        "Root, Major 3rd, Perfect 5th",         "Bright, stable, happy", "Pop, rock, folk, classical"),
            ["Minor"]              = new([0, 3, 7],        "Root, Minor 3rd, Perfect 5th",         "Dark, sad, introspective", "Rock, metal, classical, jazz"),
            ["Dominant 7"]         = new([0, 4, 7, 10],    "Major triad + Minor 7th",               "Bluesy, wants to resolve", "Blues, jazz, funk"),
            ["Major 7"]            = new([0, 4, 7, 11],    "Major triad + Major 7th",               "Sophisticated, jazzy, floating", "Jazz, contemporary, film scores"),
            ["Minor 7"]            = new([0, 3, 7, 10],    "Minor triad + Minor 7th",               "Mellow, soulful, introspective", "Jazz, soul, funk, R&B"),
            ["Minor 7b5"]          = new([0, 3, 6, 10],    "Diminished triad + Minor 7th",          "Tense, unstable, needing resolution", "Jazz (ii-V), classical"),
            ["Diminished"]         = new([0, 3, 6],        "Root, Minor 3rd, Diminished 5th",      "Very dark, unstable, rarely used", "Metal, classical, passing chord"),
            ["Diminished 7"]       = new([0, 3, 6, 9],     "Diminished triad + Diminished 7th",    "Extremely dark and unstable", "Jazz, classical, horror film scores"),
            ["Augmented"]          = new([0, 4, 8],        "Root, Major 3rd, Augmented 5th",       "Unstable, dreamy, surreal", "Classical, jazz, avant-garde"),
            ["Suspended 2"]        = new([0, 2, 7],        "Root, Major 2nd, Perfect 5th",         "Open, unresolved, folk-like", "Pop, rock, folk"),
            ["Suspended 4"]        = new([0, 5, 7],        "Root, Perfect 4th, Perfect 5th",       "Unresolved, waiting for resolution", "Pop, rock, classical"),
            ["6 Chord"]            = new([0, 4, 7, 9],     "Major triad + Major 6th",               "Warm, vintage, jazzy", "Jazz, soul, vintage pop"),
            ["Minor 6"]            = new([0, 3, 7, 9],     "Minor triad + Major 6th",               "Soft, bittersweet, sophisticated", "Jazz, classical, soul"),
        };

    // ── IOrchestratorSkill ────────────────────────────────────────────────────

    public bool CanHandle(string message)
    {
        var q = message.ToLowerInvariant();
        return (ChordQualityPattern.IsMatch(message) &&
                (q.Contains("what") || q.Contains("chord") || q.Contains("quality") || q.Contains("explain"))) ||
               (SpecificChordPattern.IsMatch(message) &&
                (q.Contains("note") || q.Contains("voic") || q.Contains("play") || q.Contains("consist")));
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var q = message.ToLowerInvariant();

        // Case 1: Chord quality explanation ("What is a major 7th?", "Explain dominant 7")
        var qualityMatch = ChordQualityPattern.Match(message);
        if (qualityMatch.Success && !q.Contains("notes in") && !q.Contains("voic"))
            return Task.FromResult(ExplainChordQuality(qualityMatch.Value));

        // Case 2: Specific chord notes ("C major", "F#m7b5", "what notes in Gmaj7?")
        var chordMatch = SpecificChordPattern.Match(message);
        if (chordMatch.Success)
        {
            var rootStr = chordMatch.Groups[1].Value;
            var qualityStr = chordMatch.Groups.Count > 2 ? chordMatch.Groups[2].Value : null;
            return Task.FromResult(ExplainSpecificChord(rootStr, qualityStr));
        }

        return Task.FromResult(CannotHelp("Could not identify a chord in your question."));
    }

    // ── Response builders ─────────────────────────────────────────────────────

    private AgentResponse ExplainChordQuality(string qualityName)
    {
        var normalizedName = NormalizeQualityName(qualityName);
        if (!ChordQualities.TryGetValue(normalizedName, out var data))
            return CannotHelp($"Unknown chord quality: {qualityName}");

        var sb = new StringBuilder();
        sb.AppendLine($"## {normalizedName} Chord");
        sb.AppendLine();
        sb.AppendLine($"**Structure:** {data.Structure}");
        var intervalNames = new[] { "Root", "2nd", "3rd", "4th", "5th", "6th", "7th" };
        var intervalStr = string.Join(", ", data.Intervals.Select((i, idx) =>
            $"{i} semitone{(i > 0 ? "s" : "")} ({intervalNames[idx]})"));
        sb.AppendLine($"**Intervals from root:** {intervalStr}");
        sb.AppendLine();
        sb.AppendLine($"**Character:** {data.Character}");
        sb.AppendLine($"**Common in:** {data.CommonUse}");
        sb.AppendLine();
        sb.AppendLine($"### Examples");
        sb.AppendLine($"- **C {normalizedName}**: {BuildChordNotes("C", data)}");
        sb.AppendLine($"- **F {normalizedName}**: {BuildChordNotes("F", data)}");
        sb.AppendLine($"- **G {normalizedName}**: {BuildChordNotes("G", data)}");

        logger.LogDebug("ChordExplanationSkill: explained {Quality} chord quality", normalizedName);

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"{normalizedName} chord: {data.Structure}",
                $"Character: {data.Character}",
                $"Uses: {data.CommonUse}",
            ],
            Assumptions = []
        };
    }

    private AgentResponse ExplainSpecificChord(string rootStr, string? qualityStr)
    {
        if (!RootPcMap.TryGetValue(rootStr, out var rootPc))
            return CannotHelp($"Unrecognised root note: {rootStr}");

        // Parse chord quality from string (e.g., "maj7", "m7b5", "7")
        var quality = ParseChordQuality(qualityStr);
        if (!ChordQualities.TryGetValue(quality.Name, out var data))
            return CannotHelp($"Unsupported chord quality: {quality.Symbol ?? "major"}");

        var notes = data.Intervals
            .Select(semitone =>
            {
                var pc = (rootPc + semitone) % 12;
                return RootNames[pc];
            })
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"## {rootStr}{quality.Symbol}");
        sb.AppendLine();
        sb.AppendLine($"**Notes:** {string.Join(", ", notes)}");
        sb.AppendLine($"**Intervals:** {data.Structure}");
        sb.AppendLine($"**Character:** {data.Character}");
        sb.AppendLine($"**Common uses:** {data.CommonUse}");
        sb.AppendLine();
        sb.AppendLine($"### Common Voicings");
        sb.AppendLine($"- **Root position:** {string.Join(" – ", notes)}");
        if (notes.Count > 2)
        {
            var firstInversion = new List<string> { ..notes.Skip(1), notes[0] };
            sb.AppendLine($"- **First inversion:** {string.Join(" – ", firstInversion)}");
        }

        logger.LogDebug("ChordExplanationSkill: {Root}{Quality} = [{Notes}]",
            rootStr, quality.Symbol, string.Join(", ", notes));

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"{rootStr}{quality.Symbol}: {string.Join(", ", notes)}",
                $"Quality: {quality.Name}",
                $"Character: {data.Character}",
            ],
            Assumptions = []
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string BuildChordNotes(string root, ChordQualityData data)
    {
        if (!RootPcMap.TryGetValue(root, out var rootPc))
            return "[unknown]";

        var notes = data.Intervals
            .Select(semitone =>
            {
                var pc = (rootPc + semitone) % 12;
                return RootNames[pc];
            })
            .ToList();

        return string.Join(", ", notes);
    }

    private static string NormalizeQualityName(string qualityName)
    {
        var q = qualityName.ToLowerInvariant().Trim();
        return q switch
        {
            "major"               => "Major",
            "minor"               => "Minor",
            "dominant" or "dom"   => "Dominant 7",
            "dominant 7" or "v7"  => "Dominant 7",
            "major 7" or "maj7"   => "Major 7",
            "minor 7" or "m7"     => "Minor 7",
            "half-diminished" or "half diminished" or "m7b5" => "Minor 7b5",
            "diminished 7" or "dim7" => "Diminished 7",
            "diminished"          => "Diminished",
            "augmented"           => "Augmented",
            "suspended 2" or "sus2" => "Suspended 2",
            "suspended 4" or "sus4" => "Suspended 4",
            "6"                   => "6 Chord",
            "minor 6" or "m6"     => "Minor 6",
            _                     => qualityName
        };
    }

    private (string Name, string? Symbol) ParseChordQuality(string? qualityStr)
    {
        if (string.IsNullOrWhiteSpace(qualityStr))
            return ("Major", null);

        var q = qualityStr.Trim().ToLowerInvariant();
        return q switch
        {
            "maj" or "maj7"  => ("Major 7", "maj7"),
            "m" or "min"     => ("Minor", "m"),
            "m7"             => ("Minor 7", "m7"),
            "m7b5"           => ("Minor 7b5", "m7b5"),
            "7"              => ("Dominant 7", "7"),
            "dim"            => ("Diminished", "dim"),
            "dim7"           => ("Diminished 7", "dim7"),
            "aug"            => ("Augmented", "aug"),
            "sus2"           => ("Suspended 2", "sus2"),
            "sus4"           => ("Suspended 4", "sus4"),
            "6"              => ("6 Chord", "6"),
            "m6"             => ("Minor 6", "m6"),
            _                => ("Major", q)
        };
    }

    private static AgentResponse CannotHelp(string reason) => new()
    {
        AgentId     = AgentIds.Theory,
        Result      = reason,
        Confidence  = 0.0f,
        Evidence    = [],
        Assumptions = ["Request could not be resolved from domain model"]
    };

    // ── Data record ───────────────────────────────────────────────────────────

    private sealed record ChordQualityData(
        IReadOnlyList<int> Intervals,
        string Structure,
        string Character,
        string CommonUse);
}
