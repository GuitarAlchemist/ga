namespace GA.Business.ML.Search;

using System.Collections.Immutable;
using GA.Domain.Core.Instruments.Biomechanics;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using Domain.Services.Fretboard.Biomechanics;

/// <summary>
///     The shared comfort / ergonomic predicate — the analysis-dependent filter seam every voicing
///     search strategy crosses, sibling to <see cref="VoicingFilterEngine"/>. Lifted out of the GPU
///     adapter because it never needed the GPU: <see cref="BiomechanicalAnalyzer"/> is pure C# and only
///     needs the fret diagram, which CPU/GPU (<c>VoicingEmbedding.Diagram</c>) and OPTK
///     (<c>OptickMetadata.Diagram</c>) all carry. So CPU, GPU, and OPTK now apply identical comfort
///     filtering. See <c>docs/adr/0002-voicing-filter-parity-cpu-gpu-only.md</c>.
/// </summary>
/// <remarks>
///     <b>Opposite unknown-bias to <see cref="VoicingFilterEngine"/> by design.</b> Metadata filtering
///     is <i>strict</i> (a voicing missing the attribute fails the filter); comfort filtering is
///     <i>lenient</i> (a diagram we cannot parse <b>passes</b> — don't punish a voicing for an analysis
///     we couldn't run). Recorded in CONTEXT.md as the "two filter seams, opposite unknown-bias" term.
/// </remarks>
public static class VoicingComfortFilter
{
    /// <summary>
    ///     True when at least one comfort / ergonomic filter is populated. Callers gate on this to skip
    ///     constructing the analyzer entirely when no comfort filter was requested.
    /// </summary>
    public static bool IsActive(VoicingSearchFilters filters) =>
        filters.MinComfortScore.HasValue || (filters.MustBeErgonomic ?? false);

    /// <summary>
    ///     True if <paramref name="diagram"/> satisfies the populated comfort filters in
    ///     <paramref name="filters"/>. Lenient: a null / empty / unparseable diagram returns
    ///     <see langword="true"/> (we keep what we can't analyze).
    /// </summary>
    public static bool Matches(string? diagram, VoicingSearchFilters filters)
    {
        if (!IsActive(filters)) return true;
        if (string.IsNullOrWhiteSpace(diagram)) return true;

        var positions = ParseDiagramToPositions(diagram);
        if (positions.Count == 0) return true; // lenient: keep voicings we can't analyze

        var analyzer = new BiomechanicalAnalyzer(filters.HandSize ?? HandSize.Medium);
        var analysis = analyzer.AnalyzeChordPlayability(positions);

        if (filters.MinComfortScore.HasValue && analysis.Comfort < filters.MinComfortScore.Value)
        {
            return false;
        }

        if ((filters.MustBeErgonomic ?? false) &&
            analysis.WristPostureAnalysis is { IsErgonomic: false })
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Parse a fret diagram into biomechanical positions. Handles both corpus formats: dash-delimited
    ///     (<c>"x-3-2-0-1-0"</c>) and compact single-char (<c>"x35453"</c>, single-digit frets only). The
    ///     old GPU-private parser split on <c>'-'</c> only, so it silently produced zero positions for the
    ///     compact format → comfort filtering became a no-op for those voicings.
    /// </summary>
    internal static ImmutableList<Position> ParseDiagramToPositions(string diagram)
    {
        var parts = diagram.Contains('-')
            ? diagram.Split('-')
            : [.. diagram.Select(c => c.ToString())];

        var positions = new List<Position>();

        // Standard tuning open MIDI notes, high-E (string 1) to low-E (string 6).
        var openMidiNotes = new[] { 64, 59, 55, 50, 45, 40 };

        for (var i = 0; i < parts.Length && i < 6; i++)
        {
            var part = parts[i].Trim();
            var str = new Str(i + 1); // Str is 1-based (strings 1-6)

            if (part is "x" or "X" or "m" or "M")
            {
                positions.Add(new Position.Muted(str));
            }
            // Range-guard the fret BEFORE constructing Fret/MidiNote: both throw on out-of-range input
            // (Fret outside [-1,36], MidiNote outside [0,127]), and int.TryParse accepts multi-digit /
            // negative dash-format tokens. A throw here would fault the whole HybridSearchAsync task
            // (it runs inside Parallel.ForEach / the kernel filter), collapsing the entire query — the
            // exact failure class VoicingFilterEngine's 2026-05-30 null-safety fix exists to prevent.
            // An out-of-range token is instead skipped → at worst the diagram yields zero positions,
            // which Matches() treats leniently (keep). 0..24 covers any real fretted guitar voicing.
            else if (int.TryParse(part, out var fretValue) && fretValue is >= 0 and <= 24)
            {
                var location = new PositionLocation(str, new Fret(fretValue));
                var midiNoteValue = i < openMidiNotes.Length ? openMidiNotes[i] + fretValue : 60 + fretValue;
                positions.Add(new Position.Played(location, new MidiNote(midiNoteValue)));
            }
        }

        return [.. positions];
    }
}
