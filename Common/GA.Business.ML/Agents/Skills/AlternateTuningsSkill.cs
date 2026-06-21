namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Domain-backed alternate-tuning skill. Answers tuning-info questions and
/// shape questions for common alternate tunings (DADGAD, drop-D, open-G, open-D,
/// double drop-D, DGCGCD, half-step down, whole-step down).
/// Surface forms:
/// <list type="bullet">
/// <item><b>"What is DADGAD tuning"</b> → string-by-string note layout + interval analysis</item>
/// <item><b>"What's drop-D tuning"</b> → DADGBE</item>
/// <item><b>"How do I tune to open G"</b> → DGDGBD with notes per string</item>
/// <item><b>"What does an Em shape look like in drop-D"</b> → caveat about altered low E string</item>
/// </list>
/// Zero LLM calls — pure tuning-table lookup + interval analysis. Confidence = 1.0.
/// </summary>
/// <remarks>
/// Built 2026-05-14 to close BACKLOG dealbreaker #2. Tier-1 deterministic per
/// <c>docs/plans/2026-05-13-skills-domain-backed-refactor-plan.md</c>.
/// String ordering follows guitarist convention: <b>low → high</b> (6th → 1st).
/// </remarks>
public sealed class AlternateTuningsSkill(ILogger<AlternateTuningsSkill> logger) : IOrchestratorSkill
{
    public string Name => "AlternateTunings";
    public string Description =>
        "Answers alternate-tuning questions: what is DADGAD / drop-D / open-G / " +
        "open-D / double-drop-D / DGCGCD / half-step-down / whole-step-down. " +
        "Returns the string-by-string layout (low → high) and explains the " +
        "intervallic difference from standard tuning. Pure lookup — no LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "what is DADGAD tuning",
        "what's drop D tuning",
        "how do I tune to open G",
        "open D tuning notes",
        "what is double drop D tuning",
        "what's half step down tuning",
        "whole step down tuning notes",
        "DGCGCD tuning explained",
        "how is DADGAD different from standard",
        "what tuning is Eb Ab Db Gb Bb Eb",
    ];

    public bool CanHandle(string message) => false;  // semantic-routing only

    // Family of patterns matching tuning names. Each one keys into Tunings[].
    private static readonly (Regex Pattern, string Key)[] TuningPatterns =
    [
        // DADGAD — contiguous or hyphenated
        (new Regex(@"\b(?:dadgad|d-a-d-g-a-d)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "dadgad"),
        // Drop D — but NOT "double drop D"
        (new Regex(@"(?<!\bdouble\s)(?<!\bdouble-)\bdrop[\s-]?d\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "drop-d"),
        // Double drop D
        (new Regex(@"\bdouble[\s-]?drop[\s-]?d\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "double-drop-d"),
        // Open G
        (new Regex(@"\bopen[\s-]?g\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "open-g"),
        // Open D
        (new Regex(@"\bopen[\s-]?d\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "open-d"),
        // DGCGCD (Sonic Youth / Pink Floyd style)
        (new Regex(@"\bdgcgcd\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "dgcgcd"),
        // Half step down
        (new Regex(@"\bhalf[\s-]?step[\s-]?down\b|\beb\s+standard\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "half-step-down"),
        // Whole step down
        (new Regex(@"\bwhole[\s-]?step[\s-]?down\b|\bd\s+standard\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "whole-step-down"),
    ];

    private static readonly Dictionary<string, TuningInfo> Tunings = new(StringComparer.Ordinal)
    {
        ["dadgad"] = new(
            DisplayName: "DADGAD",
            Notes: ["D", "A", "D", "G", "A", "D"],
            VsStandard: "Strings 6, 2 dropped a whole step (E→D, B→A); string 1 dropped a whole step (E→D). Strings 5, 4, 3 unchanged.",
            Flavor: "Open Dsus4 chord when strummed unfretted. Beloved for Celtic, modal folk, and Indian-flavored playing (Pierre Bensusan, Davey Graham, Jimmy Page on 'Black Mountain Side')."),

        ["drop-d"] = new(
            DisplayName: "Drop D",
            Notes: ["D", "A", "D", "G", "B", "E"],
            VsStandard: "Only string 6 is changed — dropped from E down a whole step to D. Strings 5–1 are standard tuning.",
            Flavor: "Easy power chords on the bottom three strings (one-finger barre). Used everywhere from Soundgarden to Foo Fighters to classical fingerstyle."),

        ["double-drop-d"] = new(
            DisplayName: "Double Drop D",
            Notes: ["D", "A", "D", "G", "B", "D"],
            VsStandard: "Strings 6 and 1 both dropped a whole step (E→D). Strings 5–2 are standard.",
            Flavor: "Neil Young's signature ('Cinnamon Girl', 'Cortez the Killer'). Both Ds frame any voicing with a fat low-and-high D drone."),

        ["open-g"] = new(
            DisplayName: "Open G",
            Notes: ["D", "G", "D", "G", "B", "D"],
            VsStandard: "Strings 6, 5, and 1 dropped a whole step (E→D, A→G, E→D). Strings 4, 3, 2 unchanged.",
            Flavor: "Open G major chord when strummed unfretted. Keith Richards's tuning (often without the low D string entirely) — the engine behind 'Start Me Up', 'Brown Sugar', 'Honky Tonk Women'."),

        ["open-d"] = new(
            DisplayName: "Open D",
            Notes: ["D", "A", "D", "F#", "A", "D"],
            VsStandard: "Strings 6, 3, 2, 1 changed: E→D, G→F#, B→A, E→D. Strings 5 and 4 unchanged.",
            Flavor: "Open D major chord when strummed unfretted. Slide-blues classic — Joni Mitchell, Bonnie Raitt, Ry Cooder, Elmore James. Bottleneck slide barring any fret gives a major chord."),

        ["dgcgcd"] = new(
            DisplayName: "DGCGCD",
            Notes: ["D", "G", "C", "G", "C", "D"],
            VsStandard: "Strings 6→1: D, G, C, G, C, D — heavily modal/quartal. Strings 6, 5, 4, 2, 1 changed; only string 3 (G) matches standard.",
            Flavor: "Sonic Youth signature variant. Quartal stacking gives huge ringing drones; you can barre across with one finger and get sus-style chords almost anywhere."),

        ["half-step-down"] = new(
            DisplayName: "Half-step down (Eb standard)",
            Notes: ["Eb", "Ab", "Db", "Gb", "Bb", "Eb"],
            VsStandard: "Every string dropped exactly 1 semitone (half step). Relative intervals identical to standard — all standard chord shapes work, the whole guitar just sounds 1 fret lower.",
            Flavor: "Jimi Hendrix's primary tuning. Easier on the voice (vocalists love it), looser strings for bigger bends. Stevie Ray Vaughan also tuned this way."),

        ["whole-step-down"] = new(
            DisplayName: "Whole-step down (D standard)",
            Notes: ["D", "G", "C", "F", "A", "D"],
            VsStandard: "Every string dropped exactly 2 semitones (whole step). All standard shapes still work — guitar sounds 2 frets lower.",
            Flavor: "Heavy / doom / sludge staple — Black Sabbath ('Master of Reality'), Soundgarden, modern stoner rock. Combined with thicker strings for tension."),
    };

    private static readonly string[] StandardTuning = ["E", "A", "D", "G", "B", "E"];

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var msg = message ?? string.Empty;

        // Try named tuning patterns first
        foreach (var (pattern, key) in TuningPatterns)
        {
            if (pattern.IsMatch(msg) && Tunings.TryGetValue(key, out var tuning))
                return Task.FromResult(AnswerTuning(tuning));
        }

        // Try reverse-lookup: user gave a 6-note tuning ("Eb Ab Db Gb Bb Eb") — find a name
        if (TryParseSixNoteTuning(msg) is { } parsed)
            return Task.FromResult(AnswerByNotes(parsed));

        return Task.FromResult(CannotHandle());
    }

    private AgentResponse AnswerTuning(TuningInfo t)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**{t.DisplayName} tuning** (low → high): **{string.Join(" – ", t.Notes)}**");
        sb.AppendLine();
        sb.AppendLine("| String | Note | vs Standard |");
        sb.AppendLine("|--------|------|-------------|");
        for (var i = 0; i < 6; i++)
        {
            var stringNum = 6 - i;
            var positionLabel = i switch
            {
                0 => " (low)",
                5 => " (high)",
                _ => string.Empty,
            };
            var diffLabel = DescribeDelta(StandardTuning[i], t.Notes[i]);
            sb.AppendLine($"| {stringNum}{positionLabel} | {t.Notes[i]} | {diffLabel} |");
        }
        sb.AppendLine();
        sb.AppendLine($"**Change from standard**: {t.VsStandard}");
        sb.AppendLine();
        sb.AppendLine($"**Why use it**: {t.Flavor}");

        return Result(sb.ToString(), $"tuning({t.DisplayName})");
    }

    private AgentResponse AnswerByNotes(string[] notes)
    {
        // Find a match
        foreach (var t in Tunings.Values)
        {
            if (NotesMatch(t.Notes, notes))
                return AnswerTuning(t);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"That tuning (low → high: **{string.Join(" – ", notes)}**) doesn't match any named alternate tuning I know.");
        sb.AppendLine();
        sb.AppendLine("Per-string difference from standard (E–A–D–G–B–E):");
        sb.AppendLine();
        sb.AppendLine("| String | Note | vs Standard |");
        sb.AppendLine("|--------|------|-------------|");
        for (var i = 0; i < 6; i++)
        {
            sb.AppendLine($"| {6 - i} | {notes[i]} | {DescribeDelta(StandardTuning[i], notes[i])} |");
        }
        return Result(sb.ToString(), $"tuning(unnamed: {string.Join(" ", notes)})");
    }

    private static bool NotesMatch(string[] a, string[] b)
    {
        if (a.Length != b.Length) return false;
        for (var i = 0; i < a.Length; i++)
        {
            if (NormalizeNote(a[i]) != NormalizeNote(b[i])) return false;
        }
        return true;
    }

    private static string NormalizeNote(string raw)
    {
        var ascii = raw.Replace("♯", "#").Replace("♭", "b");
        return ascii.Length switch
        {
            1 => ascii.ToUpperInvariant(),
            2 => char.ToUpperInvariant(ascii[0]) + ascii[1].ToString().ToLowerInvariant(),
            _ => ascii,
        };
    }

    /// <summary>
    /// Try to parse a sequence of six note tokens from the message, e.g.
    /// "Eb Ab Db Gb Bb Eb" or "E-A-D-G-B-E".
    /// </summary>
    private static string[]? TryParseSixNoteTuning(string msg)
    {
        var matches = Regex.Matches(msg, @"\b[A-Ga-g][b#♭♯]?\b");
        if (matches.Count < 6) return null;
        // Take the first 6 — accept that if the user mentions extra pitches
        // elsewhere in the prompt we might pick up garbage. Common phrasings
        // tend to put the 6 notes right next to each other.
        var arr = new string[6];
        for (var i = 0; i < 6; i++) arr[i] = NormalizeNote(matches[i].Value);
        return arr;
    }

    private static readonly Dictionary<string, int> PcLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["C"] = 0, ["C#"] = 1, ["Db"] = 1,
        ["D"] = 2, ["D#"] = 3, ["Eb"] = 3,
        ["E"] = 4,
        ["F"] = 5, ["F#"] = 6, ["Gb"] = 6,
        ["G"] = 7, ["G#"] = 8, ["Ab"] = 8,
        ["A"] = 9, ["A#"] = 10, ["Bb"] = 10,
        ["B"] = 11,
    };

    private static string DescribeDelta(string standard, string alt)
    {
        if (!PcLookup.TryGetValue(standard, out var stdPc) ||
            !PcLookup.TryGetValue(alt, out var altPc))
            return alt == standard ? "—" : "(?)";

        var diff = ((altPc - stdPc) % 12 + 12) % 12;
        var signed = diff <= 6 ? diff : diff - 12;
        return signed switch
        {
            0   => "same",
            > 0 => $"+{signed}st",
            _   => $"{signed}st",
        };
    }

    private AgentResponse Result(string text, string evidence)
    {
        logger.LogDebug("AlternateTuningsSkill: {Evidence}", evidence);
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = text,
            Confidence = 1.0f,
            Evidence   = ["Source: AlternateTuningsSkill (tuning-table lookup, low→high convention)", evidence],
        };
    }

    private static AgentResponse CannotHandle() => new()
    {
        AgentId    = AgentIds.Theory,
        Result     = "Ask about a named alternate tuning (DADGAD, drop-D, open-G, open-D, double-drop-D, DGCGCD, half-step-down, whole-step-down), or give 6 notes low→high.",
        Confidence = 0.1f,
        Evidence   = ["AlternateTuningsSkill: no recognised tuning name or note sequence"],
    };

    private sealed record TuningInfo(
        string DisplayName,
        string[] Notes,
        string VsStandard,
        string Flavor);
}
