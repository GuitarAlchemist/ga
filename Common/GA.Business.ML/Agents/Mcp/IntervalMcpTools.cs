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
    [McpServerTool(Name = "ga_interval_compute"), Description(
        "Compute the simple interval between two notes (e.g. lowerNote='C', upperNote='G' returns a perfect fifth). " +
        "Use this whenever a user asks for the interval, distance, or semitone count between two named pitches. " +
        "Accepts standard note names with optional accidentals: C, F#, Bb, etc.")]
    public static IntervalResult IntervalCompute(
        [Description("The lower note name (e.g. 'C', 'F#', 'Bb').")] string lowerNote,
        [Description("The upper note name (e.g. 'G', 'A#', 'Eb').")] string upperNote)
    {
        if (!IntervalNaming.TryParseNote(lowerNote, out var note1))
            return IntervalResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(lowerNote)}' as a note name. Try C, F#, Bb, etc.");
        if (!IntervalNaming.TryParseNote(upperNote, out var note2))
            return IntervalResult.Failure($"Could not parse '{McpEchoSanitizer.SanitizeEcho(upperNote)}' as a note name. Try C, F#, Bb, etc.");

        var interval = note1.GetInterval(note2);
        return new IntervalResult
        {
            LowerNote   = IntervalNaming.FormatNote(lowerNote),
            UpperNote   = IntervalNaming.FormatNote(upperNote),
            Name        = interval.Name,
            Quality     = IntervalNaming.QualityLongName(interval.Quality.ToString()),
            Size        = IntervalNaming.SizeOrdinalName(interval.Size.Value),
            Semitones   = interval.Semitones.Value,
        };
    }
}

/// <summary>
/// Structured result of <see cref="IntervalMcpTools.IntervalCompute"/>. The shape
/// is JSON-serialised for the LLM so it can read every field directly without
/// re-parsing prose.
/// </summary>
/// <remarks>
/// <b>Invariant:</b> when <see cref="Error"/> is non-null all other string fields
/// are <see cref="string.Empty"/> and <see cref="Semitones"/> is <c>0</c>.
/// LLMs reading this record should branch on <see cref="Error"/> first and
/// surface the message verbatim before falling back to the structured fields.
/// </remarks>
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
