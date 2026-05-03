namespace GA.Business.ML.Agents.Mcp;

using System.ComponentModel;
using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Primitives.Notes;
using ModelContextProtocol.Server;

/// <summary>
/// MCP tool surface for interval-between-two-notes computation. Wraps the
/// existing <c>Note.Accidented.GetInterval</c> domain primitive so a SKILL.md-driven
/// LLM agent can compute intervals deterministically rather than recalling them
/// from training data.
/// </summary>
/// <remarks>
/// Discovered by <see cref="Plugins.ChatPluginHost"/> via
/// <see cref="Plugins.IChatPlugin.McpToolTypes"/>. This is the canary for the
/// MCP-tool-exposure workstream — once it ports cleanly, ChordInfo / ScaleInfo /
/// Modes follow the same pattern.
/// </remarks>
[McpServerToolType]
public sealed class IntervalMcpTools
{
    /// <summary>
    /// Computes the simple interval between two notes (e.g. C → G is a perfect fifth).
    /// </summary>
    /// <param name="lowerNote">The lower note (e.g. <c>"C"</c>, <c>"F#"</c>, <c>"Bb"</c>).</param>
    /// <param name="upperNote">The upper note (same notation).</param>
    /// <returns>
    /// An <see cref="IntervalResult"/> with name (e.g. "P5"), quality and size in
    /// long form, and the semitone count. On parse error, the result has
    /// <see cref="IntervalResult.Error"/> populated.
    /// </returns>
    [McpServerTool, Description(
        "Compute the simple interval between two notes (e.g. lowerNote='C', upperNote='G' returns a perfect fifth). " +
        "Use this whenever a user asks for the interval, distance, or semitone count between two named pitches. " +
        "Accepts standard note names with optional accidentals: C, F#, Bb, etc.")]
    public IntervalResult ComputeInterval(
        [Description("The lower note name (e.g. 'C', 'F#', 'Bb').")] string lowerNote,
        [Description("The upper note name (e.g. 'G', 'A#', 'Eb').")] string upperNote)
    {
        if (!TryParseNote(lowerNote, out var note1))
            return IntervalResult.Failure($"Could not parse '{lowerNote}' as a note name. Try C, F#, Bb, etc.");
        if (!TryParseNote(upperNote, out var note2))
            return IntervalResult.Failure($"Could not parse '{upperNote}' as a note name. Try C, F#, Bb, etc.");

        var interval = note1.GetInterval(note2);
        return new IntervalResult
        {
            LowerNote   = FormatNote(lowerNote),
            UpperNote   = FormatNote(upperNote),
            Name        = interval.Name,
            Quality     = QualityLongName(interval.Quality.ToString()),
            Size        = SizeOrdinalName(interval.Size.Value),
            Semitones   = interval.Semitones.Value,
        };
    }

    private static bool TryParseNote(string token, out Note.Accidented note)
    {
        if (Note.Sharp.TryParse(token, null, out var sharp))      { note = sharp.ToAccidented();    return true; }
        if (Note.Flat.TryParse(token, null, out var flat))        { note = flat.ToAccidented();     return true; }
        if (Note.Accidented.TryParse(token, null, out var acc))   { note = acc;                    return true; }
        note = default!;
        return false;
    }

    private static string FormatNote(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return raw;
        var head = char.ToUpperInvariant(trimmed[0]).ToString();
        return trimmed.Length == 1 ? head : head + trimmed[1..].ToLowerInvariant();
    }

    private static string QualityLongName(string shortQuality) => shortQuality switch
    {
        "P" => "perfect",
        "M" => "major",
        "m" => "minor",
        "A" => "augmented",
        "d" => "diminished",
        _   => shortQuality,
    };

    private static string SizeOrdinalName(int size) => size switch
    {
        1 => "unison",
        2 => "second",
        3 => "third",
        4 => "fourth",
        5 => "fifth",
        6 => "sixth",
        7 => "seventh",
        8 => "octave",
        _ => $"{size}th",
    };
}

/// <summary>
/// Structured result of <see cref="IntervalMcpTools.ComputeInterval"/>. The shape
/// is JSON-serialised for the LLM so it can read every field directly without
/// re-parsing prose. <see cref="Error"/> is non-null only on parse failure.
/// </summary>
public sealed record IntervalResult
{
    public string LowerNote { get; init; } = string.Empty;
    public string UpperNote { get; init; } = string.Empty;

    /// <summary>Short interval name (e.g. <c>"P5"</c>, <c>"m3"</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Quality in long form: <c>"perfect"</c>, <c>"major"</c>, <c>"minor"</c>, <c>"augmented"</c>, <c>"diminished"</c>.</summary>
    public string Quality { get; init; } = string.Empty;

    /// <summary>Size in long form: <c>"unison"</c>, <c>"second"</c>, ..., <c>"octave"</c>.</summary>
    public string Size { get; init; } = string.Empty;

    /// <summary>Number of semitones between the two notes (0–12 for simple intervals).</summary>
    public int Semitones { get; init; }

    /// <summary>Non-null when the input could not be parsed as standard note names.</summary>
    public string? Error { get; init; }

    public static IntervalResult Failure(string message) => new() { Error = message };
}
