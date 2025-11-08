namespace GuitarChordProgressionMCTS;

public class MusicalElement(string name, List<int[]> voicings, HashSet<int> chordTones)
{
    public string Name { get; set; } = name; // e.g., "C Major"
    public List<int[]> Voicings { get; set; } = voicings; // List of possible voicings (string, fret)
    public HashSet<int> ChordTones { get; set; } = chordTones; // Set of MIDI note numbers in the chord

    // Constructor

    // Clone method to create a deep copy
    public MusicalElement Clone()
    {
        return new MusicalElement(
            Name,
            [..Voicings.Select(v => (int[])v.Clone())],
            [..ChordTones]
        );
    }

    // Override ToString() for easy display
    public override string ToString()
    {
        return Name;
    }
}
