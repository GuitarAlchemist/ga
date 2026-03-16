namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Tonal;
using KeyNote = GA.Domain.Core.Primitives.Notes.Note.KeyNote;

/// <summary>
/// Explains major scale modes (Ionian through Locrian) with notes, character, and parent key —
/// zero LLM calls, pure domain computation.
/// </summary>
/// <remarks>
/// Handles three query shapes:
/// <list type="bullet">
/// <item>"What are the modes of the major scale?" — lists all 7 from C major as reference.</item>
/// <item>"D Dorian" / "F# Lydian" — specific root + mode name, finds parent key.</item>
/// <item>"What is Dorian?" / "Explain Phrygian" — mode name only, shows C-reference example.</item>
/// </list>
/// </remarks>
public sealed class ModeExplorationSkill(ILogger<ModeExplorationSkill> logger) : IOrchestratorSkill
{
    public string Name        => "ModeExploration";
    public string Description => "Explains major scale modes (Ionian–Locrian) with notes, character, and parent key";

    // ── Patterns ──────────────────────────────────────────────────────────────

    private static readonly Regex ModeNamePattern = new(
        @"\b(ionian|dorian|phrygian|lydian|mixolydian|aeolian|locrian)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RootAndModePattern = new(
        @"\b([A-G][#b]?)\s+(ionian|dorian|phrygian|lydian|mixolydian|aeolian|locrian)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── Mode educational data ─────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<string, ModeData> Modes =
        new Dictionary<string, ModeData>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ionian"]     = new(1, "Bright, happy, stable",                    "The major scale itself — no alterations",              "Pop, folk, classical, country"),
            ["Dorian"]     = new(2, "Minor but lifted, sophisticated",           "Like natural minor with a raised ♭6 → major 6th",      "Jazz, blues, folk, funk"),
            ["Phrygian"]   = new(3, "Dark, exotic, flamenco",                    "Like natural minor with a lowered ♭2nd",               "Metal, flamenco, Spanish music"),
            ["Lydian"]     = new(4, "Dreamy, ethereal, floating",                "Like major with a raised ♯4th",                        "Film scores, jazz, dream pop"),
            ["Mixolydian"] = new(5, "Bright with a bluesy edge",                 "Like major with a lowered ♭7th",                       "Rock, blues, folk, Celtic"),
            ["Aeolian"]    = new(6, "Sad, melancholic, introspective",           "The natural minor scale — the standard minor mode",    "Pop, rock, folk, classical"),
            ["Locrian"]    = new(7, "Very dark, unstable, rarely used",          "Like minor with lowered ♭2nd AND ♭5th",                "Metal, jazz (rare), avant-garde"),
        };

    private static readonly string[] ModeOrder =
        ["Ionian", "Dorian", "Phrygian", "Lydian", "Mixolydian", "Aeolian", "Locrian"];

    private static readonly IReadOnlyDictionary<string, int> RootPcMap =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["C"] = 0, ["B#"] = 0,  ["C#"] = 1, ["Db"] = 1,
            ["D"] = 2,              ["D#"] = 3, ["Eb"] = 3,
            ["E"] = 4, ["Fb"] = 4,  ["F"] = 5,  ["E#"] = 5,
            ["F#"] = 6, ["Gb"] = 6, ["G"] = 7,
            ["G#"] = 8, ["Ab"] = 8, ["A"] = 9,
            ["A#"] = 10, ["Bb"] = 10, ["B"] = 11, ["Cb"] = 11,
        };

    // ── IOrchestratorSkill ────────────────────────────────────────────────────

    public bool CanHandle(string message)
    {
        if (ModeNamePattern.IsMatch(message)) return true;
        var q = message.ToLowerInvariant();
        return q.Contains("modes of") || q.Contains("all modes") ||
               q.Contains("list modes") || q.Contains("list all modes");
    }

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var q = message.ToLowerInvariant();

        // Case 1: list all 7 modes
        if (q.Contains("modes of") || q.Contains("all modes") || q.Contains("list modes"))
            return Task.FromResult(BuildAllModesResponse());

        // Case 2: "D Dorian", "F# Lydian" — specific root + mode
        var specificMatch = RootAndModePattern.Match(message);
        if (specificMatch.Success)
            return Task.FromResult(BuildSpecificModeResponse(
                specificMatch.Groups[1].Value, specificMatch.Groups[2].Value));

        // Case 3: just a mode name — "What is Dorian?", "Explain Phrygian"
        var modeMatch = ModeNamePattern.Match(message);
        if (modeMatch.Success)
            return Task.FromResult(BuildReferenceResponse(modeMatch.Value));

        return Task.FromResult(CannotHelp("Could not determine which mode you're asking about."));
    }

    // ── Response builders ─────────────────────────────────────────────────────

    private AgentResponse BuildAllModesResponse()
    {
        var cMajor = Key.Items.FirstOrDefault(k =>
            k.KeyMode == KeyMode.Major && k.Root.ToString() == "C");
        if (cMajor is null) return CannotHelp("Could not load C major key data.");

        var notes = cMajor.Notes.ToList();
        var sb = new StringBuilder();
        sb.AppendLine("## The 7 Modes of the Major Scale");
        sb.AppendLine();
        sb.AppendLine("All derived from the **C major** scale: C D E F G A B");
        sb.AppendLine();

        var evidence = new List<string> { "Reference key: C major" };
        foreach (var name in ModeOrder)
        {
            var info = Modes[name];
            var modeNotes = RotateFrom(notes, info.Degree - 1);
            var noteStr = string.Join(" ", modeNotes.Select(n => n.ToString()));
            sb.AppendLine($"### {info.Degree}. {name} ({Roman(info.Degree)})");
            sb.AppendLine($"**Notes:** {noteStr}");
            sb.AppendLine($"**Character:** {info.Character}");
            sb.AppendLine($"**Vs. major:** {info.VsIonian}");
            sb.AppendLine($"**Uses:** {info.CommonUse}");
            sb.AppendLine();
            evidence.Add($"{name}: {noteStr} — {info.Character}");
        }

        logger.LogDebug("ModeExplorationSkill: listed all 7 modes of C major");
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   = [.. evidence],
            Assumptions = ["Example from C major scale as reference"]
        };
    }

    private AgentResponse BuildSpecificModeResponse(string rootStr, string modeName)
    {
        if (!Modes.TryGetValue(modeName, out var info))
            return CannotHelp($"Unknown mode: {modeName}");

        if (!RootPcMap.TryGetValue(rootStr, out var rootPc))
            return CannotHelp($"Unrecognised root note: {rootStr}");

        var degreeIdx = info.Degree - 1;
        var parentKey = Key.Items.FirstOrDefault(k =>
            k.KeyMode == KeyMode.Major &&
            k.Notes.ToList() is { Count: 7 } ns &&
            ns[degreeIdx].PitchClass.Value == rootPc);

        if (parentKey is null)
            return CannotHelp(
                $"Could not find a standard major key with {rootStr} as degree {info.Degree} ({modeName}). " +
                "Try a note like C, D, Eb, F#, or Bb.");

        var notes = parentKey.Notes.ToList();
        var modeNotes = RotateFrom(notes, degreeIdx);
        var noteStr = string.Join(" ", modeNotes.Select(n => n.ToString()));
        var parentKeyName = $"{parentKey.Root} major";
        var capitalMode = Capitalize(modeName);

        var sb = new StringBuilder();
        sb.AppendLine($"## {rootStr} {capitalMode}");
        sb.AppendLine();
        sb.AppendLine($"**Notes:** {noteStr}");
        sb.AppendLine($"**Parent key:** {parentKeyName} (this is degree {Roman(info.Degree)} of {parentKeyName})");
        sb.AppendLine($"**Character:** {info.Character}");
        sb.AppendLine($"**Vs. major scale:** {info.VsIonian}");
        sb.AppendLine($"**Common uses:** {info.CommonUse}");

        logger.LogDebug("ModeExplorationSkill: {Root} {Mode} → [{Notes}] (parent: {Parent})",
            rootStr, capitalMode, noteStr, parentKeyName);

        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"{rootStr} {capitalMode}: {noteStr}",
                $"Parent key: {parentKeyName}",
                $"Scale degree: {Roman(info.Degree)} of {parentKeyName}",
            ],
            Assumptions = []
        };
    }

    private AgentResponse BuildReferenceResponse(string modeName)
    {
        if (!Modes.TryGetValue(modeName, out var info))
            return CannotHelp($"Unknown mode: {modeName}");

        var cMajor = Key.Items.FirstOrDefault(k =>
            k.KeyMode == KeyMode.Major && k.Root.ToString() == "C");
        if (cMajor is null) return CannotHelp("Could not load C major key data.");

        var notes = cMajor.Notes.ToList();
        var modeNotes = RotateFrom(notes, info.Degree - 1);
        var noteStr = string.Join(" ", modeNotes.Select(n => n.ToString()));
        var capitalMode = Capitalize(modeName);
        var rootNote = modeNotes[0].ToString();

        var sb = new StringBuilder();
        sb.AppendLine($"## {capitalMode} Mode (degree {Roman(info.Degree)})");
        sb.AppendLine();
        sb.AppendLine($"**Example:** {rootNote} {capitalMode} — Notes: {noteStr}");
        sb.AppendLine($"**Parent key:** C major (starting from degree {info.Degree})");
        sb.AppendLine($"**Character:** {info.Character}");
        sb.AppendLine($"**Vs. major scale:** {info.VsIonian}");
        sb.AppendLine($"**Common uses:** {info.CommonUse}");
        sb.AppendLine();
        sb.AppendLine($"> **Tip:** To use {capitalMode} from any root, find the major key " +
                      $"where your root is degree {info.Degree}, then play that key's scale starting from your root.");

        logger.LogDebug("ModeExplorationSkill: {Mode} reference response", capitalMode);
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = sb.ToString().TrimEnd(),
            Confidence = 1.0f,
            Evidence   =
            [
                $"{capitalMode}: scale degree {Roman(info.Degree)} of any major scale",
                $"Example: {noteStr} (starting from {rootNote}, derived from C major)",
                $"Character: {info.Character}",
            ],
            Assumptions = ["Example shown from C major; concept applies to all keys"]
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<KeyNote> RotateFrom(List<KeyNote> notes, int startIdx)
    {
        var result = new List<KeyNote>(7);
        for (var i = 0; i < 7; i++)
            result.Add(notes[(startIdx + i) % 7]);
        return result;
    }

    private static string Roman(int degree) => degree switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV",
        5 => "V", 6 => "VI", 7 => "VII", _ => degree.ToString()
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

    private sealed record ModeData(int Degree, string Character, string VsIonian, string CommonUse);
}
