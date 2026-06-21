namespace GA.Business.ML.Agents.Mcp;

using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tool surface for chord-symbol → notes/intervals computation. Wraps the
/// pitch-class arithmetic and enharmonic-respelling logic that <see cref="Skills.ChordInfoSkill"/>
/// also uses, so an LLM-driven SKILL.md skill can call it deterministically rather
/// than recalling the notes from training data (the failure mode there is silently
/// flipping enharmonics — Db vs C#, Ab vs G# — depending on the source the model saw).
/// </summary>
/// <remarks>
/// Discovered by <see cref="Plugins.ChatPluginHost"/> via
/// <see cref="Plugins.IChatPlugin.McpToolTypes"/>. Third tool in the MCP-tool-
/// exposure workstream — same template as <see cref="IntervalMcpTools"/> /
/// <see cref="ScaleMcpTools"/>: length-guarded inputs, sanitized Error echo via
/// <see cref="McpEchoSanitizer"/>, structured result with Error-branch invariant.
/// </remarks>
[McpServerToolType]
public sealed partial class ChordMcpTools
{
    // Realistic chord symbols are <= 8 chars: "F#dim7", "Cmaj7#5", "Bbm7b5".
    // Cap at 12 to admit edge cases without inviting MB-sized abuse.
    private const int MaxChordSymbolLength = 12;

    // Root map, root/quality normalization, and the interval formula table live in the shared
    // ChordVocabulary seam (PR #102 / architecture-review candidate #3) — same source as ChordInfoSkill.
    // NaturalLetters / NaturalPitchClasses live in ChordSpelling (PR #102).

    /// <summary>
    /// Parses a chord symbol (e.g. <c>"Cmaj7"</c>, <c>"F#m"</c>, <c>"Bbdim"</c>) and
    /// returns its constituent notes, intervals, and quality.
    /// </summary>
    [McpServerTool(Name = "ga_chord_info"), Description(
        "Parse a chord symbol and return its notes, intervals, and quality. " +
        "Examples: 'Cmaj7' returns C E G B; 'F#m' returns F# A C#; 'Bbdim' returns Bb Db Fb; " +
        "'Cdim7' returns C Eb Gb Bbb; 'Bm7b5' returns B D F A. " +
        "Use this whenever a user asks for the notes / intervals / construction of a named chord. " +
        "Supports major, minor (m/min), diminished (dim), augmented (aug), dominant 7 (just '7'), " +
        "major 7 (maj7/M7), minor 7 (m7/min7), diminished 7 (dim7), and half-diminished (m7b5).")]
    public static ChordResult GetChordInfo(
        [Description("The chord symbol — root note plus optional quality suffix. Examples: 'C', 'Cm', 'Cmaj7', 'F#dim', 'Bbm7', 'Aaug', 'Cdim7', 'Bm7b5'.")]
        string chordSymbol)
    {
        if (string.IsNullOrEmpty(chordSymbol) || chordSymbol.Length > MaxChordSymbolLength)
            return ChordResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(chordSymbol)}' as a chord symbol. Try C, Cm, Cmaj7, F#dim, Bbm7, etc.");

        var match = ChordSymbolRegex().Match(chordSymbol);
        if (!match.Success)
            return ChordResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(chordSymbol)}' as a chord symbol. Try C, Cm, Cmaj7, F#dim, Bbm7, etc.");

        var root    = ChordVocabulary.NormalizeRoot(match.Groups["root"].Value);
        var quality = ChordVocabulary.NormalizeQuality(match.Groups["quality"].Value);

        if (!ChordVocabulary.TryGetPitchClass(root, out var rootPc))
            return ChordResult.Failure($"'{McpEchoSanitizer.SanitizeEcho(root)}' is not a recognised chord root. Try C, F#, Bb, etc.");

        var formula = ChordVocabulary.GetFormula(quality);
        var notes   = formula.Intervals
            .Zip(formula.LetterSteps, (interval, letterSteps) => ChordSpelling.Spell(root, rootPc + interval, letterSteps))
            .ToArray();

        return new ChordResult
        {
            Symbol    = chordSymbol.Trim(),
            Root      = root,
            Quality   = formula.Quality,
            Notes     = notes,
            Intervals = formula.IntervalNames.ToArray(),
        };
    }

    // Spell() delegates to the shared helper (PR #102) — see ChordSpelling.

    // Order matters in the alternation: longer prefixes first so `dim7` is
    // tried before `dim` and `m7b5` before `m7`. Without this ordering, input
    // "Cdim7" matches `dim` and leaves "7" unconsumed, failing the ^...$ anchor
    // and the whole regex. Same for "Cm7b5" → matches `m` and fails on "7b5".
    [GeneratedRegex(@"^(?<root>[A-Ga-g][#b]?)(?<quality>maj7|min7b5|min7|m7b5|m7|maj|min|m|dim7|dim|aug|7|M7|M)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ChordSymbolRegex();
}

/// <summary>
/// Structured result of <see cref="ChordMcpTools.GetChordInfo"/>.
/// </summary>
/// <remarks>
/// <b>Invariant:</b> when <see cref="Error"/> is non-null, all string fields
/// are <see cref="string.Empty"/> and <see cref="Notes"/> / <see cref="Intervals"/>
/// are empty. LLMs reading this record should branch on <see cref="Error"/> first.
/// </remarks>
public sealed record ChordResult
{
    /// <summary>The chord symbol as parsed (echoed back, trimmed).</summary>
    public string Symbol { get; init; } = string.Empty;

    public string Root    { get; init; } = string.Empty;
    public string Quality { get; init; } = string.Empty;

    /// <summary>Chord tones in ascending order, e.g. <c>["C","E","G","B"]</c> for Cmaj7.</summary>
    public string[] Notes { get; init; } = [];

    /// <summary>Interval names, e.g. <c>["root","major third","perfect fifth","major seventh"]</c>.</summary>
    public string[] Intervals { get; init; } = [];

    /// <summary>Non-null when the input could not be parsed as a chord symbol.</summary>
    public string? Error { get; init; }

    public static ChordResult Failure(string message) => new() { Error = message };
}
