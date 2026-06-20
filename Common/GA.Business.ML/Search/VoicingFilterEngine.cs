namespace GA.Business.ML.Search;

using GA.Domain.Services.Fretboard.Voicings.Core;

/// <summary>
///     The single authority for deciding whether a <see cref="VoicingEmbedding"/> satisfies a set of
///     <see cref="VoicingSearchFilters"/> — the metadata-filter seam the search strategies cross.
/// </summary>
/// <remarks>
///     Lifted verbatim from <see cref="CpuVoicingSearchStrategy"/>'s hardened predicate (the one with the
///     2026-05-30 null-safety fix and case-insensitive <c>Contains</c> semantics). The GPU strategy
///     historically carried its OWN, drifted copy — exact <c>==</c> instead of <c>Contains</c>,
///     case-sensitive, not null-safe, <c>Tags</c> matched ANY instead of ALL, and several metadata
///     filters (e.g. <c>SetClassId</c>, <c>FingerCount</c>, <c>ChordName</c>) were silently skipped — so
///     the same filtered query could return different results on CPU vs GPU. This type is the deep
///     module both adapters delegate to so that can't recur. Analysis-dependent filters (biomechanical
///     comfort/stretch) need a fingering-analysis service and stay layered in the GPU adapter; this
///     engine owns only the metadata predicate.
/// </remarks>
public static class VoicingFilterEngine
{
    /// <summary>
    ///     True if <paramref name="voicing"/> satisfies every populated filter in <paramref name="filters"/>.
    /// </summary>
    /// <remarks>
    ///     Null-safe by convention: a corpus voicing that lacks the filtered attribute cannot satisfy a
    ///     filter on it, so a null attribute returns false rather than throwing — before this, a single
    ///     voicing with a null VoicingType/ChordName/etc. threw inside the search's Parallel.ForEach and
    ///     collapsed the ENTIRE voicing search → orchestration error → LLM fallback for every
    ///     "&lt;chord&gt; voicing on guitar" query (live trace 2026-05-30).
    /// </remarks>
    public static bool Matches(VoicingEmbedding voicing, VoicingSearchFilters filters)
    {
        if (filters.Difficulty != null && (voicing.Difficulty == null || !voicing.Difficulty.Equals(filters.Difficulty, StringComparison.OrdinalIgnoreCase))) return false;
        if (filters.Position != null && (voicing.Position == null || !voicing.Position.Equals(filters.Position, StringComparison.OrdinalIgnoreCase))) return false;
        if (filters.VoicingType != null && (voicing.VoicingType == null || !voicing.VoicingType.Contains(filters.VoicingType, StringComparison.OrdinalIgnoreCase))) return false;
        if (filters.Tags != null && filters.Tags.Any() && (voicing.SemanticTags == null || !filters.Tags.All(t => voicing.SemanticTags.Contains(t, StringComparer.OrdinalIgnoreCase)))) return false;

        if (filters.MinFret.HasValue && voicing.MinFret < filters.MinFret.Value) return false;
        if (filters.MaxFret.HasValue && voicing.MaxFret > filters.MaxFret.Value) return false;
        if (filters.RequireBarreChord.HasValue && voicing.BarreRequired != filters.RequireBarreChord.Value) return false;

        // Structured filters
        if (filters.ChordName != null && (voicing.ChordName == null || !voicing.ChordName.Contains(filters.ChordName, StringComparison.OrdinalIgnoreCase))) return false;

        if (filters.StackingType != null)
        {
            if (voicing.StackingType == null || !voicing.StackingType.Equals(filters.StackingType, StringComparison.OrdinalIgnoreCase)) return false;
        }

        if (filters.IsSlashChord.HasValue)
        {
            var isSlash = (voicing.MidiBassNote % 12) != voicing.RootPitchClass;
            if (filters.IsSlashChord.Value != isSlash) return false;
        }

        if (filters.MinMidiPitch.HasValue && voicing.MidiNotes.Length > 0 && voicing.MidiNotes.Min() < filters.MinMidiPitch.Value) return false;
        if (filters.MaxMidiPitch.HasValue && voicing.MidiNotes.Length > 0 && voicing.MidiNotes.Max() > filters.MaxMidiPitch.Value) return false;

        if (filters.SetClassId != null && (voicing.PrimeFormId == null || !voicing.PrimeFormId.Contains(filters.SetClassId, StringComparison.OrdinalIgnoreCase))) return false;

        // PitchClassSet string looks like "{0,4,7}" or similar.
        if (filters.RahnPrimeForm != null && !voicing.PitchClassSet.Contains(filters.RahnPrimeForm, StringComparison.OrdinalIgnoreCase)) return false;

        if (filters.FingerCount.HasValue)
        {
            // Heuristic: count non-open, non-muted strings. Exact finger count needs fingering analysis
            // which is not in the search index yet, so active strings is the proxy.
            var parts = voicing.Diagram.Contains('-') ? voicing.Diagram.Split('-') : [.. voicing.Diagram.Select(c => c.ToString())];
            var active = parts.Count(p => p != "x" && p != "m" && p != "0");
            if (active != filters.FingerCount.Value) return false;
        }

        // Phase 3 extended filters
        if (filters.HarmonicFunction != null && !string.Equals(voicing.HarmonicFunction, filters.HarmonicFunction, StringComparison.OrdinalIgnoreCase)) return false;
        if (filters.IsNaturallyOccurring.HasValue && voicing.IsNaturallyOccurring != filters.IsNaturallyOccurring.Value) return false;
        if (filters.IsRootless.HasValue && voicing.IsRootless != filters.IsRootless.Value) return false;
        if (filters.HasGuideTones.HasValue && voicing.HasGuideTones != filters.HasGuideTones.Value) return false;
        if (filters.Inversion.HasValue && voicing.Inversion != filters.Inversion.Value) return false;
        if (filters.MinConsonance.HasValue && voicing.ConsonanceScore < filters.MinConsonance.Value) return false;
        if (filters.MinBrightness.HasValue && voicing.BrightnessScore < filters.MinBrightness.Value) return false;
        if (filters.MaxBrightness.HasValue && voicing.BrightnessScore > filters.MaxBrightness.Value) return false;
        if (filters.OmittedTones != null && filters.OmittedTones.Any() &&
            (voicing.OmittedTones == null || !filters.OmittedTones.All(t => voicing.OmittedTones.Contains(t, StringComparer.OrdinalIgnoreCase)))) return false;

        // Melody note filter
        if (filters.TopPitchClass.HasValue && voicing.TopPitchClass != filters.TopPitchClass.Value) return false;

        // AI-agent metadata filters (Phase 4)
        if (filters.TexturalDescriptionContains != null &&
            (voicing.TexturalDescription == null || !voicing.TexturalDescription.Contains(filters.TexturalDescriptionContains, StringComparison.OrdinalIgnoreCase))) return false;

        if (filters.DoubledTonesContain != null && filters.DoubledTonesContain.Any() &&
            (voicing.DoubledTones == null || !filters.DoubledTonesContain.All(t => voicing.DoubledTones.Contains(t, StringComparer.OrdinalIgnoreCase)))) return false;

        if (filters.AlternateNameMatch != null &&
            (voicing.AlternateNames == null || !voicing.AlternateNames.Any(n => n.Contains(filters.AlternateNameMatch, StringComparison.OrdinalIgnoreCase)))) return false;

        return true;
    }
}
