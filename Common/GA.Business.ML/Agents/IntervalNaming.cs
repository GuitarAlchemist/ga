namespace GA.Business.ML.Agents;

using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Primitives.Notes;

/// <summary>
///     Shared note-parsing and interval-naming seam crossed by both
///     <see cref="Mcp.IntervalMcpTools"/> (MCP transport adapter) and
///     <see cref="Skills.IntervalSkill"/> (orchestrator adapter).
/// </summary>
/// <remarks>
///     Both adapters previously carried byte-identical <c>QualityLongName</c> / <c>SizeOrdinalName</c>
///     tables and near-identical note parsing; only the MCP side guarded input length and only the
///     skill side carried a redundant identity <c>.Replace</c>. Consolidating here keeps the two
///     adapters in lock-step (candidate #3 of the architecture review) — the length guard now protects
///     both, and there is one quality/size naming table.
/// </remarks>
public static class IntervalNaming
{
    // Realistic note tokens are 1–3 chars (C, F#, Bbb, C##). Cap inputs at 4 to defend against
    // pathological ~MB strings — Note.{Sharp,Flat}.TryParse calls Trim().ToUpperInvariant().Replace(...),
    // each of which allocates proportional to input length before the validation predicate runs.
    private const int MaxNoteTokenLength = 4;

    /// <summary>
    ///     Parses a single note token (<c>"C"</c>, <c>"F#"</c>, <c>"Bb"</c>) into an
    ///     <see cref="Note.Accidented"/>. Length-guarded so a pathological input can't allocate
    ///     proportional intermediate buffers in the domain TryParse calls.
    /// </summary>
    public static bool TryParseNote(string? token, out Note.Accidented note)
    {
        if (string.IsNullOrEmpty(token) || token.Length > MaxNoteTokenLength)
        {
            note = default!;
            return false;
        }

        if (Note.Sharp.TryParse(token, null, out var sharp))      { note = sharp.ToAccidented(); return true; }
        if (Note.Flat.TryParse(token, null, out var flat))        { note = flat.ToAccidented();  return true; }
        if (Note.Accidented.TryParse(token, null, out var acc))   { note = acc;                  return true; }

        note = default!;
        return false;
    }

    /// <summary>Normalises a note for display: <c>"c#"</c> → <c>"C#"</c>, <c>"bB"</c> → <c>"Bb"</c>.</summary>
    public static string FormatNote(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Length == 0) return raw;
        var head = char.ToUpperInvariant(trimmed[0]).ToString();
        return trimmed.Length == 1 ? head : head + trimmed[1..].ToLowerInvariant();
    }

    /// <summary>Expands a short interval quality (<c>"P"</c>, <c>"M"</c>, <c>"m"</c>, <c>"A"</c>, <c>"d"</c>) to long form.</summary>
    public static string QualityLongName(string shortQuality) => shortQuality switch
    {
        "P" => "perfect",
        "M" => "major",
        "m" => "minor",
        "A" => "augmented",
        "d" => "diminished",
        _   => shortQuality,
    };

    /// <summary>Names an interval size ordinal (1 → unison, 5 → fifth, 8 → octave, otherwise <c>"{n}th"</c>).</summary>
    public static string SizeOrdinalName(int size) => size switch
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
