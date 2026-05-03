namespace GA.Business.ML.Agents.Mcp;

using System.ComponentModel;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Tonal;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tool surface for scale-/key-level computation. Wraps the existing
/// <see cref="Key.Items"/> domain primitive so an LLM-driven SKILL.md skill
/// can return the notes of a major or minor scale deterministically rather
/// than recalling them from training data.
/// </summary>
/// <remarks>
/// Discovered by <see cref="Plugins.ChatPluginHost"/> via
/// <see cref="Plugins.IChatPlugin.McpToolTypes"/>. Second tool in the MCP-
/// tool-exposure workstream — same template as <see cref="IntervalMcpTools"/>:
/// length-guarded inputs, sanitized Error echo, structured result with
/// <see cref="ScaleResult.Error"/> branch.
/// </remarks>
[McpServerToolType]
public sealed class ScaleMcpTools
{
    // Realistic root tokens: C, F#, Bbb — three chars max with double-flat.
    // Cap at 4 to defend against pathological MB-sized inputs without rejecting
    // the legitimate edge cases.
    private const int MaxRootTokenLength = 4;

    // Tightest realistic mode tokens are "major" / "minor" / "maj" / "min" =
    // 5 chars. Cap at 12 to leave room for variants without inviting abuse.
    private const int MaxModeTokenLength = 12;

    /// <summary>
    /// Returns the seven notes of a major or minor key together with its key
    /// signature description and relative-key name.
    /// </summary>
    [McpServerTool(Name = "ga_scale_get_notes"), Description(
        "Get the 7 notes of a major or minor key (e.g. root='C', mode='major' returns C D E F G A B). " +
        "Use this whenever a user asks 'what are the notes in X major/minor', 'show me the X scale', etc. " +
        "Accepts standard root names with optional accidentals (C, F#, Bb) and mode names 'major'/'minor' (or 'maj'/'min').")]
    public ScaleResult GetKeyNotes(
        [Description("The root note (e.g. 'C', 'F#', 'Bb'). Case-insensitive.")] string root,
        [Description("The mode — 'major' or 'minor' (also accepts 'maj' / 'min').")] string mode)
    {
        if (string.IsNullOrEmpty(root) || root.Length > MaxRootTokenLength)
            return ScaleResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(root)}' as a root note. Try C, F#, Bb, etc.");
        if (string.IsNullOrEmpty(mode) || mode.Length > MaxModeTokenLength)
            return ScaleResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(mode)}' as a mode. Use 'major' or 'minor'.");

        var modeNorm = mode.Trim().ToLowerInvariant();
        var isMinor  = modeNorm is "minor" or "min";
        var isMajor  = modeNorm is "major" or "maj";
        if (!isMinor && !isMajor)
            return ScaleResult.Failure($"Unknown mode '{McpEchoSanitizer.SanitizeEcho(mode)}'. Use 'major' or 'minor'.");

        var key = Key.Items.FirstOrDefault(k =>
            k.KeyMode == (isMinor ? KeyMode.Minor : KeyMode.Major) &&
            string.Equals(k.Root.ToString(), root.Trim(), StringComparison.OrdinalIgnoreCase));

        if (key is null)
            return ScaleResult.Failure(
                $"'{McpEchoSanitizer.SanitizeEcho(root)} {McpEchoSanitizer.SanitizeEcho(mode)}' is not a standard key. Try C major, F# minor, Bb major, etc.");

        var notes = key.Notes.Select(n => n.ToString()).ToArray();

        return new ScaleResult
        {
            Root         = key.Root.ToString(),
            Mode         = isMinor ? "minor" : "major",
            Notes        = notes,
            KeySignature = DescribeKeySignature(key),
            RelativeKey  = GetRelativeKeyName(key),
        };
    }

    private static string DescribeKeySignature(Key key)
    {
        var count = key.KeySignature.AccidentalCount;
        if (count == 0) return "no sharps or flats";
        var kind  = key.KeySignature.AccidentalKind == AccidentalKind.Sharp ? "sharp" : "flat";
        return $"{count} {kind}{(count > 1 ? "s" : "")}";
    }

    private static string GetRelativeKeyName(Key key)
    {
        // Relative pair shares the same pitch-class set — find by PC mask. Same
        // arithmetic the C# ScaleInfoSkill uses; lifted verbatim so behavior
        // matches.
        var mask = key.Notes.Aggregate(0, (acc, n) => acc | (1 << n.PitchClass.Value));
        var sibling = Key.Items.FirstOrDefault(k =>
            k.KeyMode != key.KeyMode &&
            k.Notes.Aggregate(0, (acc, n) => acc | (1 << n.PitchClass.Value)) == mask);

        return sibling is null
            ? "none"
            : $"{sibling.Root} {(sibling.KeyMode == KeyMode.Major ? "major" : "minor")}";
    }
}

/// <summary>
/// Structured result of <see cref="ScaleMcpTools.GetKeyNotes"/>.
/// </summary>
/// <remarks>
/// <b>Invariant:</b> when <see cref="Error"/> is non-null, all string fields
/// are <see cref="string.Empty"/> and <see cref="Notes"/> is empty.
/// LLMs reading this record should branch on <see cref="Error"/> first.
/// </remarks>
public sealed record ScaleResult
{
    public string Root { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;

    /// <summary>Seven scale notes in ascending order, e.g. <c>["C","D","E","F","G","A","B"]</c>.</summary>
    public string[] Notes { get; init; } = [];

    /// <summary>Human-readable key signature, e.g. <c>"2 sharps"</c> or <c>"no sharps or flats"</c>.</summary>
    public string KeySignature { get; init; } = string.Empty;

    /// <summary>The relative key name, e.g. <c>"A minor"</c> for C major.</summary>
    public string RelativeKey { get; init; } = string.Empty;

    /// <summary>Non-null when the input could not be parsed as a standard key.</summary>
    public string? Error { get; init; }

    public static ScaleResult Failure(string message) => new() { Error = message };
}
