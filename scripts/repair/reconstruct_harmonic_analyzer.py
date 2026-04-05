
namespace GA.Domain.Services.Fretboard.Voicings.Analysis;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Domain.Theory.Atonal;
using GA.Domain.Theory.Tonal;
using GA.Domain.Theory.Tonal.Scales;
using GA.Domain.Theory.Harmony;
using GA.Domain.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Instruments.Fretboard.Voicings.Analysis;
using GA.Domain.Services.Unified;
using GA.Domain.Services.Chords;
using GA.Domain.Instruments.Primitives;
using GA.Domain.Primitives;

public static class VoicingHarmonicAnalyzer
{
    public static VoicingCharacteristics Analyze(Voicing voicing)
    {
        var midiNotes = voicing.Notes.Select(n => n.Value).OrderBy(n => n).ToArray();
        var pitchClasses = voicing.Notes.Select(n => n.PitchClass).Distinct().OrderBy(pc => pc.Value).ToList();
        
        // Basic identification via PitchClassSet
        var pcSet = new PitchClassSet(pitchClasses);
        var chordId = IdentifyChord(pcSet, midiNotes[0]);

        // Calculate consonance/dissonance
        var dissonanceScore = CalculateDissonance(midiNotes);
        
        // Interval spread
        var intervalSpread = midiNotes.Last() - midiNotes.First();

        return new VoicingCharacteristics(
            chordId,
            dissonanceScore,
            intervalSpread,
            pitchClasses.Count,
            pcSet.IntervalClassVector.ToString()
        );
    }

    private static ChordIdentification IdentifyChord(PitchClassSet pcSet, int bassMidi)
    {
        // Simple identification strategy: match against known chord templates
        // This is a simplified placeholder logic.
        var templates = ChordTemplateFactory.GenerateAllPossibleChords();
        
        var match = templates.FirstOrDefault(t => t.PitchClassSet.Equals(pcSet));
        var name = match?.Name ?? "Unknown";
        var root = match != null ? PitchClass.FromValue(0) : PitchClass.FromValue(bassMidi % 12); // Placeholder root logic

        return new ChordIdentification(name, root.ToString(), "Unknown Quality");
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
