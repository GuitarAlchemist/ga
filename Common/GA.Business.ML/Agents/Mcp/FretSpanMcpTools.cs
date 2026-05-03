namespace GA.Business.ML.Agents.Mcp;

using System.ComponentModel;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tool surface for guitar chord-diagram playability analysis. Wraps the
/// pure arithmetic in <see cref="Skills.FretSpanSkill"/> so an LLM-driven
/// SKILL.md skill can compute fret span deterministically rather than
/// recalling diagrams from training data (which it cannot reliably do for
/// uncommon voicings).
/// </summary>
/// <remarks>
/// Discovered by <see cref="Plugins.ChatPluginHost"/> via
/// <see cref="Plugins.IChatPlugin.McpToolTypes"/>. Fourth tool in the MCP-
/// tool-exposure workstream — same template as <see cref="IntervalMcpTools"/>,
/// <see cref="ScaleMcpTools"/>, <see cref="ChordMcpTools"/>: length-guarded
/// inputs, sanitized Error echo via <see cref="McpEchoSanitizer"/>, structured
/// result with Error-branch invariant.
/// </remarks>
[McpServerToolType]
public sealed partial class FretSpanMcpTools
{
    // Realistic chord diagrams: 6 tokens of 1-2 chars each + 5 separators =
    // up to 17 chars. Compact form ("x02230") is 6 chars. Cap at 20 to admit
    // both styles without inviting MB-sized abuse.
    private const int MaxDiagramLength = 20;

    /// <summary>
    /// Parses a 6-string guitar chord diagram and returns its fret span,
    /// difficulty description, and playability score.
    /// </summary>
    [McpServerTool(Name = "ga_fret_span"), Description(
        "Compute fret span and playability for a 6-string guitar chord diagram. " +
        "Accepts either dash-separated (e.g. 'x-3-2-0-1-0') or compact form starting with x (e.g. 'x32010'). " +
        "Tokens are listed low-to-high (E A D G B e); 'x' = muted, '0' = open, otherwise the fret number. " +
        "Use this whenever a user asks about a chord's stretch / reach / playability / difficulty.")]
    public FretSpanResult ComputeSpan(
        [Description("The chord diagram. Examples: 'x-3-2-0-1-0' (C major), 'x32010', '0-2-2-1-0-0' (E major). Six positions, low-to-high.")]
        string diagram)
    {
        if (string.IsNullOrEmpty(diagram) || diagram.Length > MaxDiagramLength)
            return FretSpanResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(diagram)}' as a chord diagram. Try 'x-3-2-0-1-0' or 'x32010'.");

        var frets = TryParseFrets(diagram);
        if (frets is null)
            return FretSpanResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(diagram)}' as a chord diagram. Use 6 positions low-to-high (E A D G B e); 'x' = mute, '0' = open.");

        var pressed = frets.Where(f => f > 0).ToList();
        if (pressed.Count == 0)
            return FretSpanResult.Failure("All strings are open or muted — no fret span to compute.");

        var minFret = pressed.Min();
        var maxFret = pressed.Max();
        var span    = maxFret - minFret;

        var difficulty = span switch
        {
            0 or 1 => "very easy — all fretted notes sit in adjacent positions",
            2      => "easy — comfortable stretch for most players",
            3      => "moderate — a normal left-hand extension",
            4      => "challenging — requires a significant stretch; warm up first",
            _      => $"very wide — span of {span} frets may be difficult for smaller hands",
        };

        var normalized = string.Join("-", frets.Select(f => f < 0 ? "x" : f.ToString()));
        var playabilityScore = int.Clamp(1 + span * 2, 1, 10);

        return new FretSpanResult
        {
            Diagram          = normalized,
            Frets            = frets.ToArray(),
            MinFret          = minFret,
            MaxFret          = maxFret,
            Span             = span,
            Difficulty       = difficulty,
            PlayabilityScore = playabilityScore,
        };
    }

    /// <summary>
    /// Returns 6 fret values (-1 = muted, 0 = open, &gt;0 = fret number) or
    /// null if the input doesn't match a known diagram form. Lifted from
    /// <c>FretSpanSkill.TryParseFrets</c>.
    /// </summary>
    private static List<int>? TryParseFrets(string message)
    {
        var dash = DashDiagramRegex().Match(message);
        if (dash.Success)
        {
            return Enumerable.Range(1, 6)
                .Select(i => ParseFretToken(dash.Groups[i].Value))
                .ToList();
        }

        var compact = CompactDiagramRegex().Match(message);
        if (!compact.Success) return null;

        var val = compact.Value;
        return val.Select(c => c is 'x' or 'X' ? -1 : c - '0').ToList();
    }

    private static int ParseFretToken(string s) =>
        s.Equals("x", StringComparison.OrdinalIgnoreCase) ? -1 : int.Parse(s);

    [GeneratedRegex(
        @"\b([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})-([xX]|\d{1,2})\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex DashDiagramRegex();

    [GeneratedRegex(@"\b[xX]\d{5}\b", RegexOptions.CultureInvariant)]
    private static partial Regex CompactDiagramRegex();
}

/// <summary>
/// Structured result of <see cref="FretSpanMcpTools.ComputeSpan"/>.
/// </summary>
/// <remarks>
/// <b>Invariant:</b> when <see cref="Error"/> is non-null, all string fields
/// are <see cref="string.Empty"/>, <see cref="Frets"/> is empty, and the
/// numeric fields are <c>0</c>. LLMs reading this record should branch on
/// <see cref="Error"/> first.
/// </remarks>
public sealed record FretSpanResult
{
    /// <summary>Diagram normalized to dash-separated form, e.g. <c>"x-3-2-0-1-0"</c>.</summary>
    public string Diagram { get; init; } = string.Empty;

    /// <summary>Six fret values low-to-high. <c>-1</c> = muted, <c>0</c> = open, otherwise the fret number.</summary>
    public int[] Frets { get; init; } = [];

    /// <summary>Lowest fretted (non-zero, non-muted) position.</summary>
    public int MinFret { get; init; }

    /// <summary>Highest fretted position.</summary>
    public int MaxFret { get; init; }

    /// <summary>Difference between MaxFret and MinFret — the actual hand stretch required.</summary>
    public int Span { get; init; }

    /// <summary>Human-readable difficulty description.</summary>
    public string Difficulty { get; init; } = string.Empty;

    /// <summary>Coarse playability score 1 (easy) → 10 (very hard).</summary>
    public int PlayabilityScore { get; init; }

    /// <summary>Non-null when the input could not be parsed as a chord diagram.</summary>
    public string? Error { get; init; }

    public static FretSpanResult Failure(string message) => new() { Error = message };
}
