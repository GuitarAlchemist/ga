namespace GaApi.Services;

using GA.Business.Core.Analysis.Voicings;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Services.Chords.Parsing;
using GA.Domain.Services.Fretboard.Engine;
using GA.Domain.Services.Fretboard.Voicings.Analysis;
using GaApi.Models;

public class VoicingFilterService
{
    private readonly Fretboard _fretboard = Fretboard.Default;
    private readonly FretboardChordsGenerator _generator;
    private readonly ChordSymbolParser _chordParser = new();

    public VoicingFilterService() => _generator = new FretboardChordsGenerator(_fretboard);

    public async Task<IEnumerable<VoicingWithAnalysis>> GetVoicingsForChordAsync(
        string chordName,
        int?   maxDifficulty  = null,
        int?   minFret        = null,
        int?   maxFret        = null,
        bool   noOpenStrings  = false)
    {
        if (!_chordParser.TryParse(chordName, out var chord) || chord is null)
            throw new ArgumentException($"Cannot parse chord symbol: '{chordName}'");

        var positions = _generator
            .GetChordPositions(chord.PitchClassSet, requiredNotes: EssentialPitchClasses(chord))
            .ToList();
        var results   = new List<VoicingWithAnalysis>();

        foreach (var posList in positions)
        {
            var playedPositions = posList.OfType<Position.Played>().ToList();

            // ── Fret-range filter ───────────────────────────────────────────────
            if (minFret.HasValue && playedPositions.Any(p => p.Location.Fret.Value < minFret.Value))
                continue;
            if (maxFret.HasValue && playedPositions.Any(p => p.Location.Fret.Value > maxFret.Value))
                continue;

            // ── Open-string filter ──────────────────────────────────────────────
            if (noOpenStrings && playedPositions.Any(p => p.Location.Fret.Value == 0))
                continue;

            var midiNotes = playedPositions.Select(p => p.MidiNote);
            var voicing   = new Voicing([.. posList], [.. midiNotes]);
            var analysis  = VoicingAnalyzer.Analyze(voicing);

            // ── Difficulty filter ───────────────────────────────────────────────
            if (maxDifficulty.HasValue && analysis.PlayabilityInfo.DifficultyScore > maxDifficulty.Value)
                continue;

            results.Add(new VoicingWithAnalysis(
                chordName,
                [.. voicing.Positions.Select(p => p is Position.Played played ? played.Location.Fret.Value : -1)],
                analysis.PlayabilityInfo.Difficulty,
                analysis.PlayabilityInfo.DifficultyScore,
                analysis.PhysicalLayout.HandPosition,
                analysis.PhysicalLayout.StringSet,
                analysis.PlayabilityInfo.CagedShape,
                analysis.SemanticTags,
                analysis.PlayabilityInfo.BarreRequired,
                analysis.PlayabilityInfo.BarreInfo
            ));
        }

        return await Task.FromResult(results.OrderBy(r => r.DifficultyScore));
    }

    /// <summary>Most distinct pitch classes a voicing is required to sound.</summary>
    private const int MaxRequiredPitchClasses = 4;

    /// <summary>
    ///     The chord tones every voicing must sound. Triads and four-note chords need all their tones.
    ///     A larger chord (9th, 11th, 13th) cannot sound every tone on six strings within a hand span,
    ///     so it drops optional tones, as guitarists do, until four remain: first the perfect fifth,
    ///     then a natural 11th when a 13th is present (it clashes with the major third), then the 9th.
    ///     The root, third, seventh and the defining extension always stay, so C13 keeps
    ///     <c>x-3-2-3-3-5</c> and C11 keeps its 11th.
    /// </summary>
    private static PitchClassSet EssentialPitchClasses(GA.Domain.Core.Theory.Harmony.Chord chord)
    {
        var root = chord.Root.PitchClass.Value;
        var intervals = chord.PitchClassSet.Select(pc => (pc.Value - root + 12) % 12).ToHashSet();

        int[] optionalInDropOrder = intervals.Contains(9) ? [7, 5, 2] : [7, 2];
        foreach (var interval in optionalInDropOrder)
        {
            if (intervals.Count <= MaxRequiredPitchClasses) break;
            intervals.Remove(interval);
        }

        return new PitchClassSet([.. intervals.Select(i => PitchClass.FromValue((root + i) % 12))]);
    }
}
