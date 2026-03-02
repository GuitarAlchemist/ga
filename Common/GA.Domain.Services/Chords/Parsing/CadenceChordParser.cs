namespace GA.Domain.Services.Chords.Parsing;

using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Harmony;

public class CadenceChordParser
{
    private readonly Dictionary<string, ChordQuality> _suffixMap = new()
    {
        // Longer matches first
        { "m7b5", ChordQuality.HalfDiminished },
        { "dim7", ChordQuality.Diminished7 },
        { "maj7", ChordQuality.Major7 },
        { "min7", ChordQuality.Minor7 },
        { "sus4", ChordQuality.Suspended },
        { "sus2", ChordQuality.Suspended },
        { "aug", ChordQuality.Augmented },

        // Shorter matches
        { "dim", ChordQuality.Diminished },
        { "m7", ChordQuality.Minor7 },
        { "7", ChordQuality.Dominant },
        { "m", ChordQuality.Minor },
        { "min", ChordQuality.Minor },
        { "+", ChordQuality.Augmented },
        { "°", ChordQuality.Diminished },
        { "ø", ChordQuality.HalfDiminished },
        { "5", ChordQuality.Major } // Treat power chords as Major function for now
    };

    // Longer matches first
    // Shorter matches
    // Treat power chords as Major function for now

    public ChordQuality ParseQuality(string chordName)
    {
        var lowerName = chordName.ToLowerInvariant();

        foreach (var kvp in _suffixMap)
        {
            if (lowerName.Contains(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return ChordQuality.Major;
    }

    public int ParseRoot(string chordName)
    {
        var s = chordName.Trim();
        if (string.IsNullOrEmpty(s)) return 0;

        // Extract the root note part (e.g. "C#m7" -> "C#")
        // Simple heuristic: Note letter + optional accidental
        var length = 1;
        if (s.Length > 1 && (s[1] == '#' || s[1] == 'b'))
        {
            length = 2;
        }

        var rootStr = s.Substring(0, length);
        
        if (Note.Chromatic.TryParse(rootStr, null, out var note))
        {
            return note.PitchClass.Value; 
        }

        // Fallback for potential parsing errors or unknown formats
        // Logically defaulting to 0 is risky but maintains legacy behavior for now.
        return 0;
    }
}
