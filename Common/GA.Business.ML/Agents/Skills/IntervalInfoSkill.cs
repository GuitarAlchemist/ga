namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Tonal;

/// <summary>
/// Answers queries about intervals (e.g., "what is a major third?" or "how many semitones in a perfect fifth?")
/// using pure domain computation — zero LLM calls.
/// </summary>
/// <remarks>
/// Supports two modes:
/// <list type="bullet">
/// <item><b>Name-only</b>: "What is a major third?" — explains the interval with semitone count and examples.</item>
/// <item><b>Two-note</b>: "C to E" or "F# to Bb" — identifies the interval between two notes and its quality.</item>
/// </list>
/// </remarks>
public sealed class IntervalInfoSkill(ILogger<IntervalInfoSkill> logger) : IOrchestratorSkill
{
    public string Name        => "IntervalInfo";
    public string Description => "Explains intervals by name or identifies intervals between two notes";

    // ── Patterns ──────────────────────────────────────────────────────────────

    private static readonly Regex IntervalNamePattern = new(
        @"\b(unison|prime|second|third|fourth|fifth|sixth|seventh|octave)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex QualityPattern = new(
        @"\b(perfect|major|minor|augmented|diminished)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TwoNotePattern = new(
        @"([A-G][#b]?)\s+(?:to|and)\s+([A-G][#b]?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── Root note mapping ─────────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<string, int> RootPcMap =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["C"] = 0,  ["B#"] = 0,  ["C#"] = 1, ["Db"] = 1,
            ["D"] = 2,               ["D#"] = 3, ["Eb"] = 3,
            ["E"] = 4,  ["Fb"] = 4,  ["F"] = 5,  ["E#"] = 5,
            ["F#"] = 6, ["Gb"] = 6,  ["G"] = 7,
            ["G#"] = 8, ["Ab"] = 8,  ["A"] = 9,
            ["A#"] = 10, ["Bb"] = 10, ["B"] = 11, ["Cb"] = 11,
        };

    private static readonly string[] RootNames =
        ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    // ── Interval educational data ─────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<string, IntervalData> Intervals =
        new Dictionary<string, IntervalData>(StringComparer.OrdinalIgnoreCase)
        {
            ["Unison"]    = new(0,  1, "Same pitch", "Perfect unison — both notes are identical"),
            ["Prime"]     = new(0,  1, "Same pitch", "Enharmonic equivalent to unison"),
            ["Second"]    = new(1,  2, "1 or 2 semitones apart", "Half-step (minor) or whole-step (major)"),
            ["Third"]     = new(3,  3, "3 or 4 semitones apart", "Core to chord quality: major or minor"),
            ["Fourth"]    = new(5,  4, "5 semitones apart", "Consonant; basis of many harmonies"),
            ["Fifth"]     = new(7,  5, "7 semitones apart", "Consonant; essential in power chords and roots"),
            ["Sixth"]     = new(8,  6, "8 or 9 semitones apart", "Warm, often used in extensions"),
            ["Seventh"]   = new(10, 7, "10 or 11 semitones apart", "Tense; resolves inward (tritone apart)"),
            ["Octave"]    = new(12, 8, "12 semitones apart", "Same note, different register"),
        };

    // ── IOrchestratorSkill ────────────────────────────────────────────────────

    public bool CanHandle(string message)
    {
        var q = message.ToLowerInvariant();
        return (IntervalNamePattern.IsMatch(message) &&
                (q.Contains("what is") || q.Contains("interval") || q.Contains("semitone") || q.Contains("how many"))) ||
               TwoNotePattern.IsMatch(message) &&
               (q.Contains("to") || q.Contains("and") || q.Contains("interval"));
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        // Case 1: Two-note interval identification ("C to E", "F# and Bb")
        var twoNoteMatch = TwoNotePattern.Match(message);
        if (twoNoteMatch.Success)
            return Task.FromResult(IdentifyInterval(twoNoteMatch.Groups[1].Value, twoNoteMatch.Groups[2].Value));

        // Case 2: Named interval explanation ("What is a major third?")
        var qualityMatch = QualityPattern.Match(message);
        var intervalMatch = IntervalNamePattern.Match(message);

        if (intervalMatch.Success)
        {
            var quality = qualityMatch.Success ? qualityMatch.Value.ToLowerInvariant() : null;
            return Task.FromResult(ExplainInterval(intervalMatch.Value, quality));
        }

        return Task.FromResult(CannotHelp("Could not identify an interval in your question."));
    }

    // ── Response builders ─────────────────────────────────────────────────────

    private AgentResponse ExplainInterval(string intervalName, string? quality)
    {
        if (!Intervals.TryGetValue(intervalName, out var info))
            return CannotHelp($"Unknown interval: {intervalName}");

        var sb = new StringBuilder();
        sb.AppendLine($"## {Capitalize(intervalName)} Interval");
        sb.AppendLine();
        sb.AppendLine($"**Size:** {info.Description}");
        sb.AppendLine($"**Semitones:** {info.Semitones}");

        // Provide examples based on quality
        if (quality?.ToLowerInvariant() is "major" or "minor")
        {
            if (intervalName.Equals("Second", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine(quality.Equals("major", StringComparison.OrdinalIgnoreCase)
                    ? $"**Examples:** C → D, F → G, A → B (whole step = 2 semitones)"
                    : $"**Examples:** C → Db, E → F, B → C (half step = 1 semitone)");
            }
            else if (intervalName.Equals("Third", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine(quality.Equals("major", StringComparison.OrdinalIgnoreCase)
                    ? $"**Examples:** C → E, F → A, G → B (4 semitones)"
                    : $"**Examples:** C → Eb, D → F, E → G (3 semitones)");
            }
            else if (intervalName.Equals("Sixth", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine(quality.Equals("major", StringComparison.OrdinalIgnoreCase)
                    ? $"**Examples:** C → A, F → D, G → E (9 semitones)"
                    : $"**Examples:** C → Ab, E → C, G → Eb (8 semitones)");
            }
            else if (intervalName.Equals("Seventh", StringComparison.OrdinalIgnoreCase))
            {
                sb.AppendLine(quality.Equals("major", StringComparison.OrdinalIgnoreCase)
                    ? $"**Examples:** C → B, F → E, G → F# (11 semitones)"
                    : $"**Examples:** C → Bb, E → D, G → F (10 semitones)");
            }
        }
        else
        {
            sb.AppendLine($"**Examples for {Capitalize(intervalName)}:** {BuildExamples(intervalName)}");
        }

        sb.AppendLine();
        sb.AppendLine($"**Explanation:** {info.Explanation}");

        // Add harmonic context
        if (intervalName.Equals("Fifth", StringComparison.OrdinalIgnoreCase) ||
            intervalName.Equals("Fourth", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"**Context:** Consonant interval — stable and pleasant-sounding.");
        else if (intervalName.Equals("Seventh", StringComparison.OrdinalIgnoreCase) ||
                 intervalName.Equals("Second", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"**Context:** Dissonant interval — creates tension and typically resolves inward.");

        logger.LogDebug("IntervalInfoSkill: explained {Interval}{Quality}",
            intervalName, quality is not null ? $" ({quality})" : "");

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"{Capitalize(intervalName)}: {info.Semitones} semitones",
                $"Description: {info.Description}",
                $"Quality: {quality ?? "varies by context"}",
            ],
            Assumptions = []
        };
    }

    private AgentResponse IdentifyInterval(string note1Str, string note2Str)
    {
        if (!RootPcMap.TryGetValue(note1Str, out var pc1) || !RootPcMap.TryGetValue(note2Str, out var pc2))
            return CannotHelp($"Could not identify one or both notes: {note1Str}, {note2Str}");

        var semitones = (pc2 - pc1 + 12) % 12;
        var (intervalName, quality) = ClassifyInterval(semitones);

        var sb = new StringBuilder();
        sb.AppendLine($"## Interval: {note1Str} to {note2Str}");
        sb.AppendLine();
        sb.AppendLine($"**Interval:** {quality} {intervalName}");
        sb.AppendLine($"**Semitones:** {semitones}");
        sb.AppendLine($"**Direction:** Ascending from {note1Str} up to {note2Str}");

        if (Intervals.TryGetValue(intervalName, out var info))
            sb.AppendLine($"**Explanation:** {info.Explanation}");

        logger.LogDebug("IntervalInfoSkill: {Note1} to {Note2} = {Quality} {Interval}",
            note1Str, note2Str, quality, intervalName);

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"{note1Str} to {note2Str}: {quality} {intervalName}",
                $"Semitones: {semitones}",
                $"Pitch class distance: {(pc2 - pc1 + 12) % 12}",
            ],
            Assumptions = ["Ascending interval; octave not considered"]
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (string IntervalName, string Quality) ClassifyInterval(int semitones)
    {
        return semitones switch
        {
            0  => ("Unison", "Perfect"),
            1  => ("Second", "Minor"),
            2  => ("Second", "Major"),
            3  => ("Third", "Minor"),
            4  => ("Third", "Major"),
            5  => ("Fourth", "Perfect"),
            6  => ("Tritone", "Augmented"),
            7  => ("Fifth", "Perfect"),
            8  => ("Sixth", "Minor"),
            9  => ("Sixth", "Major"),
            10 => ("Seventh", "Minor"),
            11 => ("Seventh", "Major"),
            12 => ("Octave", "Perfect"),
            _  => ("Unknown", "Unknown"),
        };
    }

    private static string BuildExamples(string intervalName) =>
        intervalName.ToLowerInvariant() switch
        {
            "unison"  => "C–C (same note)",
            "second"  => "C–D (major), C–Db (minor)",
            "third"   => "C–E (major), C–Eb (minor)",
            "fourth"  => "C–F (5 semitones)",
            "fifth"   => "C–G (7 semitones)",
            "sixth"   => "C–A (major), C–Ab (minor)",
            "seventh" => "C–B (major), C–Bb (minor)",
            "octave"  => "C–C' (one octave up)",
            _         => "",
        };

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..].ToLowerInvariant();

    private static AgentResponse CannotHelp(string reason) => new()
    {
        AgentId     = AgentIds.Theory,
        Result      = reason,
        Confidence  = 0.0f,
        Evidence    = [],
        Assumptions = ["Request could not be resolved from domain model"]
    };

    // ── Data record ───────────────────────────────────────────────────────────

    private sealed record IntervalData(int Semitones, int Degree, string Description, string Explanation);
}
