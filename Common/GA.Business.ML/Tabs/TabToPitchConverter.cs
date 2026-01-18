namespace GA.Business.ML.Tabs;

using Models;

/// <summary>
/// Converts tab positions (String, Fret) into absolute pitches (MIDI numbers).
/// Defaulting to Standard Tuning (E A D G B e).
/// </summary>
public class TabToPitchConverter
{
    // Standard Tuning MIDI offsets (Low E to High e)
    // String 0 (Low E) = 40 (E2)
    // String 1 (A)     = 45 (A2)
    // String 2 (D)     = 50 (D3)
    // String 3 (G)     = 55 (G3)
    // String 4 (B)     = 59 (B3)
    // String 5 (High e)= 64 (E4)
    private static readonly int[] StandardTuningOffsets = { 40, 45, 50, 55, 59, 64 };

    public List<int> GetMidiNotes(TabSlice slice)
    {
        var midiNotes = new List<int>();
        if (slice.IsEmpty || slice.IsBarLine) return midiNotes;

        foreach (var note in slice.Notes)
        {
            if (note.StringIndex >= 0 && note.StringIndex < StandardTuningOffsets.Length)
            {
                int baseMidi = StandardTuningOffsets[note.StringIndex];
                int finalMidi = baseMidi + note.Fret;
                midiNotes.Add(finalMidi);
            }
        }
        
        return midiNotes.Distinct().OrderBy(x => x).ToList();
    }

    public List<int> GetPitchClasses(TabSlice slice)
    {
        return GetMidiNotes(slice).Select(m => m % 12).Distinct().OrderBy(pc => pc).ToList();
    }
}
