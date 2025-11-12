namespace GA.Business.Core.Fretboard.Voicings.Filtering;

using Analysis;
using Core;

/// <summary>
/// Filters voicings based on specified criteria
/// </summary>
public static class VoicingFilters
{
    /// <summary>
    /// Check if a voicing matches the specified criteria
    /// </summary>
    public static bool MatchesCriteria(Voicing voicing, MusicalVoicingAnalysis analysis, VoicingFilterCriteria criteria)
    {
        return MatchesCheapFilters(voicing, criteria) && MatchesExpensiveFilters(voicing, analysis, criteria);
    }

    /// <summary>
    /// Cheap filters that don't require musical analysis (just voicing structure)
    /// </summary>
    private static bool MatchesCheapFilters(Voicing voicing, VoicingFilterCriteria criteria)
    {
        // These filters are fast - they only look at voicing structure
        if (!MatchesFretRange(voicing, criteria.FretRange)) return false;
        if (!MatchesNoteCount(voicing, criteria.NoteCount)) return false;

        return true;
    }

    /// <summary>
    /// Expensive filters that require full musical analysis
    /// </summary>
    private static bool MatchesExpensiveFilters(
        Voicing voicing,
        MusicalVoicingAnalysis analysis,
        VoicingFilterCriteria criteria)
    {
        // These filters require analysis - they examine chord names, keys, etc.
        if (!MatchesChordType(analysis, criteria.ChordType)) return false;
        if (!MatchesVoicingType(analysis, criteria.VoicingType)) return false;
        if (!MatchesCharacteristics(analysis, criteria.Characteristics)) return false;
        if (!MatchesKeyContext(analysis, criteria.KeyContext)) return false;

        return true;
    }

    private static bool MatchesChordType(MusicalVoicingAnalysis analysis, ChordTypeFilter? filter)
    {
        if (filter == null || filter == ChordTypeFilter.All) return true;

        var chordName = analysis.ChordId.ChordName?.ToLower() ?? "";

        return filter switch
        {
            ChordTypeFilter.Triads => analysis.MidiNotes.Select(n => n.PitchClass).Distinct().Count() == 3 &&
                                     !chordName.Contains("7") && !chordName.Contains("9") &&
                                     !chordName.Contains("11") && !chordName.Contains("13"),
            ChordTypeFilter.SeventhChords => chordName.Contains("7") && !chordName.Contains("9") &&
                                            !chordName.Contains("11") && !chordName.Contains("13"),
            ChordTypeFilter.ExtendedChords => chordName.Contains("9") || chordName.Contains("11") ||
                                             chordName.Contains("13"),
            ChordTypeFilter.MajorChords => chordName.Contains("maj") ||
                                          (!chordName.Contains("m") && !chordName.Contains("dim") &&
                                           !chordName.Contains("aug") && !chordName.Contains("sus")),
            ChordTypeFilter.MinorChords => chordName.Contains("m") && !chordName.Contains("maj") &&
                                          !chordName.Contains("dim"),
            ChordTypeFilter.DominantChords => chordName.Contains("7") && !chordName.Contains("maj7") &&
                                             !chordName.Contains("m7"),
            ChordTypeFilter.DiminishedChords => chordName.Contains("dim") || chordName.Contains("°"),
            ChordTypeFilter.AugmentedChords => chordName.Contains("aug") || chordName.Contains("+"),
            ChordTypeFilter.SuspendedChords => chordName.Contains("sus"),
            _ => true
        };
    }

    private static bool MatchesVoicingType(MusicalVoicingAnalysis analysis, VoicingTypeFilter? filter)
    {
        if (filter == null || filter == VoicingTypeFilter.All) return true;

        var dropVoicing = analysis.VoicingCharacteristics.DropVoicing;
        var isRootless = analysis.VoicingCharacteristics.IsRootless;
        var isOpen = analysis.VoicingCharacteristics.IsOpenVoicing;

        return filter switch
        {
            VoicingTypeFilter.Drop2 => dropVoicing == "Drop-2",
            VoicingTypeFilter.Drop3 => dropVoicing == "Drop-3",
            VoicingTypeFilter.Drop2And4 => dropVoicing == "Drop-2+4",
            VoicingTypeFilter.Rootless => isRootless,
            VoicingTypeFilter.ShellVoicings => analysis.MidiNotes.Select(n => n.PitchClass).Distinct().Count() == 3 &&
                                               isRootless, // Shell voicings are typically rootless 3-note voicings
            VoicingTypeFilter.ClosedPosition => !isOpen,
            VoicingTypeFilter.OpenPosition => isOpen,
            _ => true
        };
    }

    private static bool MatchesCharacteristics(MusicalVoicingAnalysis analysis, VoicingCharacteristicFilter? filter)
    {
        if (filter == null || filter == VoicingCharacteristicFilter.All) return true;

        var isOpen = analysis.VoicingCharacteristics.IsOpenVoicing;
        var isRootless = analysis.VoicingCharacteristics.IsRootless;
        var features = analysis.VoicingCharacteristics.Features;

        return filter switch
        {
            VoicingCharacteristicFilter.OpenVoicingsOnly => isOpen,
            VoicingCharacteristicFilter.ClosedVoicingsOnly => !isOpen,
            VoicingCharacteristicFilter.RootlessOnly => isRootless,
            VoicingCharacteristicFilter.WithRootOnly => !isRootless,
            VoicingCharacteristicFilter.QuartalHarmony => features.Any(f => f.Contains("Quartal")),
            VoicingCharacteristicFilter.SuspendedChords => features.Any(f => f.Contains("Suspended")),
            VoicingCharacteristicFilter.AddedToneChords => features.Any(f => f.Contains("Added tones")),
            _ => true
        };
    }

    private static bool MatchesKeyContext(MusicalVoicingAnalysis analysis, KeyContextFilter? filter)
    {
        if (filter == null || filter == KeyContextFilter.All) return true;

        var isNaturallyOccurring = analysis.ChordId.IsNaturallyOccurring;
        var hasChromaticNotes = analysis.ChromaticNotes != null && analysis.ChromaticNotes.Count > 0;
        var closestKey = analysis.ChordId.ClosestKey;

        return filter switch
        {
            KeyContextFilter.DiatonicOnly => isNaturallyOccurring && !hasChromaticNotes,
            KeyContextFilter.ChromaticOnly => hasChromaticNotes,
            KeyContextFilter.InKeyOfC => closestKey?.ToString().Contains("C") ?? false,
            KeyContextFilter.InKeyOfG => closestKey?.ToString().Contains("G") ?? false,
            KeyContextFilter.InKeyOfD => closestKey?.ToString().Contains("D") ?? false,
            KeyContextFilter.InKeyOfA => closestKey?.ToString().Contains("A") ?? false,
            KeyContextFilter.InKeyOfE => closestKey?.ToString().Contains("E") ?? false,
            KeyContextFilter.InKeyOfF => closestKey?.ToString().Contains("F") ?? false,
            KeyContextFilter.InKeyOfBb => closestKey?.ToString().Contains("Bb") ?? false,
            KeyContextFilter.InKeyOfEb => closestKey?.ToString().Contains("Eb") ?? false,
            _ => true
        };
    }

    private static bool MatchesFretRange(Voicing voicing, FretRangeFilter? filter)
    {
        if (filter == null || filter == FretRangeFilter.All) return true;

        var maxFret = voicing.Positions
            .OfType<Primitives.Position.Played>()
            .Select(p => p.Location.Fret.Value)
            .DefaultIfEmpty(0)
            .Max();

        return filter switch
        {
            FretRangeFilter.OpenPosition => maxFret <= 4,
            FretRangeFilter.MiddlePosition => maxFret >= 5 && maxFret <= 12,
            FretRangeFilter.UpperPosition => maxFret > 12,
            _ => true
        };
    }

    private static bool MatchesNoteCount(Voicing voicing, NoteCountFilter? filter)
    {
        if (filter == null || filter == NoteCountFilter.All) return true;

        var noteCount = voicing.Notes.Select(n => n.PitchClass).Distinct().Count();

        return filter switch
        {
            NoteCountFilter.TwoNotes => noteCount == 2,
            NoteCountFilter.ThreeNotes => noteCount == 3,
            NoteCountFilter.FourNotes => noteCount == 4,
            NoteCountFilter.FiveOrMore => noteCount >= 5,
            _ => true
        };
    }
}

