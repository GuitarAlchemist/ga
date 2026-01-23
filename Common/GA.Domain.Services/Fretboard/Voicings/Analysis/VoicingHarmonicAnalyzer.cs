
namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Analysis;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Common;
using Chords;

public static class VoicingHarmonicAnalyzer
{
    public static VoicingCharacteristics Analyze(Voicing voicing)
    {
        var midiNotes = voicing.Notes.Select(n => n.Value).OrderBy(n => n).ToArray();
        var pitchClasses = voicing.Notes.Select(n => n.PitchClass).Distinct().OrderBy(pc => pc.Value).ToList();
        
        // Basic identification via PitchClassSet
        var pcSet = new PitchClassSet(pitchClasses);
        var chordId = IdentifyChord(pcSet, pitchClasses, PitchClass.FromValue(midiNotes[0]));

        // Calculate consonance/dissonance
        var dissonanceScore = CalculateDissonance(midiNotes);
        
        // Interval spread
        var intervalSpread = midiNotes.Last() - midiNotes.First();

        return new VoicingCharacteristics(
            chordId,
            dissonanceScore,
            intervalSpread,
            pitchClasses.Count,
            pcSet.IntervalClassVector.ToString(), false, null, false, new List<string>()
        );
    }

    public static ChordIdentification IdentifyChord(PitchClassSet pcSet, IEnumerable<PitchClass> notes, PitchClass bassNote)
    {
        var templates = ChordTemplateFactory.GenerateAllPossibleChords().ToList();
        
        // Prioritize the bass note as the primary potential root
        var rootCandidates = notes.Distinct().OrderBy(n => n.Value == bassNote.Value ? 0 : 1).ThenBy(n => n.Value).ToList();

        foreach (var potentialRoot in rootCandidates)
        {
            var transposedId = pcSet.Id.Transpose(12 - potentialRoot.Value);
            var match = templates.FirstOrDefault(t => t.PitchClassSet.Id.Value == transposedId.Value);

            if (match != null)
            {
                return new ChordIdentification(
                    match.Name, 
                    potentialRoot.ToString(), 
                    match.Quality.ToString(), 
                    true, 
                    AnalysisConstants.FunctionalHarmony, 
                    match.Quality.ToString(), 
                    null, 
                    null);
            }
        }

        // Fallback
        return new ChordIdentification(AnalysisConstants.Unknown, bassNote.ToString(), AnalysisConstants.UnknownQuality, true, "Function", "Quality", null, null);
    }

    private static double CalculateDissonance(int[] midiNotes)
    {
        // Placeholder dissonance calculation (e.g. based on intervals)
        double dissonance = 0;
        for (int i = 0; i < midiNotes.Length; i++)
        {
            for (int j = i + 1; j < midiNotes.Length; j++)
            {
                var interval = Math.Abs(midiNotes[i] - midiNotes[j]);
                if (interval % 12 == 1 || interval % 12 == 6) dissonance += 1.0; // Minor 2nd or Tritone
            }
        }
        return dissonance;
    }
}
