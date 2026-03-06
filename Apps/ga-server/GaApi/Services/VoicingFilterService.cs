namespace GaApi.Services;

using GA.Business.Core.Analysis.Voicings;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Services.Fretboard.Analysis;
using GA.Domain.Services.Fretboard.Engine;
using GA.Domain.Services.Fretboard.Voicings.Analysis;
using GaApi.Models;

public class VoicingFilterService
{
    private readonly Fretboard _fretboard = Fretboard.Default;
    private readonly FretboardChordsGenerator _generator;

    public VoicingFilterService() => _generator = new FretboardChordsGenerator(_fretboard);

    public async Task<IEnumerable<VoicingWithAnalysis>> GetVoicingsForChordAsync(string chordName, int? maxDifficulty = null)
    {
        // For now, simpler implementation: parse chord name to PitchClassSet
        var pcs = ParseChordToPitchClassSet(chordName);
        
        var positions = _generator.GetChordPositions(pcs).ToList();
        var results = new List<VoicingWithAnalysis>();

        foreach (var posList in positions)
        {
            var midiNotes = posList
                .OfType<Position.Played>()
                .Select(p => p.MidiNote);

            var voicing = new Voicing([.. posList], [.. midiNotes]);
            var analysis = VoicingAnalyzer.Analyze(voicing);

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

    private PitchClassSet ParseChordToPitchClassSet(string chordName)
    {
        // Stub: This should use a proper chord parser
        if (chordName.StartsWith("C")) return new PitchClassSet([0, 4, 7]);
        if (chordName.StartsWith("G")) return new PitchClassSet([7, 11, 2]);
        if (chordName.StartsWith("D")) return new PitchClassSet([2, 6, 9]);
        if (chordName.StartsWith("A")) return new PitchClassSet([9, 1, 4]);
        if (chordName.StartsWith("E")) return new PitchClassSet([4, 8, 11]);
        
        return new PitchClassSet([0, 4, 7]); // Fallback to C Major
    }
}
