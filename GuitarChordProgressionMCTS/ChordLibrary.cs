namespace GuitarChordProgressionMCTS;

public static class ChordLibrary
{
    // Returns a dictionary of chords in the given key with their possible voicings and chord tones
    public static Dictionary<string, MusicalElement> GetChordsInKey(string key)
    {
        var chords = new Dictionary<string, MusicalElement>
        {
            // Example for the key of C Major
            // Chord tones are MIDI note numbers
            {
                "C Major", new MusicalElement("C Major",
                    [[-1, 3, 2, 0, 1, 0]],
                    [60, 64, 67])
            }, // C E G
            {
                "D Minor", new MusicalElement("D Minor",
                    [[-1, -1, 0, 2, 3, 1]],
                    [62, 65, 69])
            }, // D F A
            {
                "E Minor", new MusicalElement("E Minor",
                    [[0, 2, 2, 0, 0, 0]],
                    [64, 67, 71])
            }, // E G B
            {
                "F Major", new MusicalElement("F Major",
                    [[-1, -1, 3, 2, 1, 1]],
                    [65, 69, 72])
            }, // F A C
            {
                "G Major", new MusicalElement("G Major",
                    [[3, 2, 0, 0, 0, 3]],
                    [67, 71, 74])
            }, // G B D
            {
                "A Minor", new MusicalElement("A Minor",
                    [[-1, 0, 2, 2, 1, 0]],
                    [69, 72, 76])
            }, // A C E
            {
                "B Diminished", new MusicalElement("B Diminished",
                    [[-1, 2, 3, 4, 3, -1]],
                    [71, 74, 77])
            }, // B D F
            // Non-diatonic and extended chords
            {
                "E7", new MusicalElement("E7",
                    [[0, 2, 0, 1, 0, 0]],
                    [64, 68, 71, 74])
            }, // E G# B D
            {
                "F Minor", new MusicalElement("F Minor",
                    [[-1, -1, 3, 1, 1, 1]],
                    [65, 68, 72])
            }, // F Ab C
            {
                "C Major 7", new MusicalElement("C Major 7",
                    [[-1, 3, 2, 0, 0, 0]],
                    [60, 64, 67, 71])
            }, // C E G B
            {
                "A Minor 9", new MusicalElement("A Minor 9",
                    [[-1, 0, 5, 5, 0, 0]],
                    [69, 72, 76, 79, 83])
            } // A C E G B
        };

        // ... Add more chords as needed

        return chords;
    }
}
