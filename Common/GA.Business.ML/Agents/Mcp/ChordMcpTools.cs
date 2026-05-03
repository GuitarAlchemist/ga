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

    private static readonly IReadOnlyDictionary<string, int> PitchClasses =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["C"] = 0,  ["C#"] = 1, ["Db"] = 1,
            ["D"] = 2,  ["D#"] = 3, ["Eb"] = 3,
            ["E"] = 4,  ["F"] = 5,  ["F#"] = 6, ["Gb"] = 6,
            ["G"] = 7,  ["G#"] = 8, ["Ab"] = 8,
            ["A"] = 9,  ["A#"] = 10, ["Bb"] = 10,
            ["B"] = 11, ["B#"] = 0, ["Cb"] = 11, ["E#"] = 5, ["Fb"] = 4,
        };

    private static readonly char[] NaturalLetters = ['C', 'D', 'E', 'F', 'G', 'A', 'B'];
    private static readonly IReadOnlyDictionary<char, int> NaturalPitchClasses = new Dictionary<char, int>
    {
        ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11,
    };

    /// <summary>
    /// Parses a chord symbol (e.g. <c>"Cmaj7"</c>, <c>"F#m"</c>, <c>"Bbdim"</c>) and
    /// returns its constituent notes, intervals, and quality.
    /// </summary>
    [McpServerTool(Name = "ga_chord_info"), Description(
        "Parse a chord symbol and return its notes, intervals, and quality. " +
        "Examples: 'Cmaj7' returns C E G B; 'F#m' returns F# A C#; 'Bbdim' returns Bb Db Fb. " +
        "Use this whenever a user asks for the notes / intervals / construction of a named chord. " +
        "Supports major, minor (m/min), diminished (dim), augmented (aug), dominant 7 (just '7'), " +
        "major 7 (maj7/M7), and minor 7 (m7/min7).")]
    public ChordResult GetChordInfo(
        [Description("The chord symbol — root note plus optional quality suffix. Examples: 'C', 'Cm', 'Cmaj7', 'F#dim', 'Bbm7', 'Aaug'.")]
        string chordSymbol)
    {
        if (string.IsNullOrEmpty(chordSymbol) || chordSymbol.Length > MaxChordSymbolLength)
            return ChordResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(chordSymbol)}' as a chord symbol. Try C, Cm, Cmaj7, F#dim, Bbm7, etc.");

        var match = ChordSymbolRegex().Match(chordSymbol);
        if (!match.Success)
            return ChordResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(chordSymbol)}' as a chord symbol. Try C, Cm, Cmaj7, F#dim, Bbm7, etc.");

        var root    = NormalizeRoot(match.Groups["root"].Value);
        var quality = NormalizeQuality(match.Groups["quality"].Value);

        if (!PitchClasses.TryGetValue(root, out var rootPc))
            return ChordResult.Failure($"'{McpEchoSanitizer.SanitizeEcho(root)}' is not a recognised chord root. Try C, F#, Bb, etc.");

        var formula = GetFormula(quality);
        var notes   = formula.Intervals
            .Zip(formula.LetterSteps, (interval, letterSteps) => Spell(root, rootPc + interval, letterSteps))
            .ToArray();

        return new ChordResult
        {
            Symbol    = chordSymbol.Trim(),
            Root      = root,
            Quality   = formula.DisplayName,
            Notes     = notes,
            Intervals = formula.IntervalNames.ToArray(),
        };
    }

    private static string NormalizeRoot(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.Length switch
        {
            0 => string.Empty,
            1 => trimmed.ToUpperInvariant(),
            _ => char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant(),
        };
    }

    private static string NormalizeQuality(string raw) =>
        raw.Trim().ToLowerInvariant() switch
        {
            ""                              => "major",
            "maj" or "major" or "M"         => "major",
            "m" or "min" or "minor"         => "minor",
            "dim" or "diminished"           => "diminished",
            "aug" or "augmented"            => "augmented",
            "7" or "dominant" or "dom7"     => "dominant 7",
            "maj7" or "major 7" or "M7"     => "major 7",
            "m7" or "min7" or "minor 7"     => "minor 7",
            var other                       => other,
        };

    private static ChordFormula GetFormula(string quality) => quality switch
    {
        "minor"      => new("minor",      [0, 3, 7],     [0, 2, 4],    ["root", "minor third", "perfect fifth"]),
        "diminished" => new("diminished", [0, 3, 6],     [0, 2, 4],    ["root", "minor third", "diminished fifth"]),
        "augmented"  => new("augmented",  [0, 4, 8],     [0, 2, 4],    ["root", "major third", "augmented fifth"]),
        "dominant 7" => new("dominant 7", [0, 4, 7, 10], [0, 2, 4, 6], ["root", "major third", "perfect fifth", "minor seventh"]),
        "major 7"    => new("major 7",    [0, 4, 7, 11], [0, 2, 4, 6], ["root", "major third", "perfect fifth", "major seventh"]),
        "minor 7"    => new("minor 7",    [0, 3, 7, 10], [0, 2, 4, 6], ["root", "minor third", "perfect fifth", "minor seventh"]),
        _            => new("major",      [0, 4, 7],     [0, 2, 4],    ["root", "major third", "perfect fifth"]),
    };

    /// <summary>
    /// Enharmonic-aware note spelling: given a root, target pitch class, and the
    /// number of letter-steps from root to target, picks the right letter +
    /// accidental so a Cmaj chord spells C-E-G (not C-E-Abb) and a Bbm chord
    /// spells Bb-Db-F (not Bb-C#-F).
    /// </summary>
    private static string Spell(string root, int pitchClass, int letterSteps)
    {
        var rootLetter   = char.ToUpperInvariant(root[0]);
        var rootIndex    = Array.IndexOf(NaturalLetters, rootLetter);
        var targetLetter = NaturalLetters[(rootIndex + letterSteps) % NaturalLetters.Length];
        var targetNatural = NaturalPitchClasses[targetLetter];
        var normalized   = ((pitchClass % 12) + 12) % 12;
        var accidental   = ((normalized - targetNatural) % 12 + 12) % 12;

        return accidental switch
        {
            0  => targetLetter.ToString(),
            1  => $"{targetLetter}#",
            2  => $"{targetLetter}##",
            10 => $"{targetLetter}bb",
            11 => $"{targetLetter}b",
            _  => $"{targetLetter}{(accidental < 6 ? new string('#', accidental) : new string('b', 12 - accidental))}",
        };
    }

    [GeneratedRegex(@"^(?<root>[A-Ga-g][#b]?)(?<quality>maj7|min7|m7|maj|min|m|dim|aug|7|M7|M)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ChordSymbolRegex();

    private sealed record ChordFormula(
        string DisplayName,
        IReadOnlyList<int> Intervals,
        IReadOnlyList<int> LetterSteps,
        IReadOnlyList<string> IntervalNames);
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
