namespace GA.Business.Core.Tonal.Cadences;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Chords;

public class CadenceChordParser
{
    private readonly Dictionary<string, ChordQuality> _suffixMap;

    public CadenceChordParser()
    {
        _suffixMap = new Dictionary<string, ChordQuality>
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
    }

    public ChordQuality ParseQuality(string chordName)
    {
        var lowerName = chordName.ToLowerInvariant();
        
        foreach (var kvp in _suffixMap)
        {
            if (lowerName.Contains(kvp.Key)) return kvp.Value;
        }

        return ChordQuality.Major;
    }

    public int ParseRoot(string chordName)
    {
        // ... (Logic from CadenceDetector)
        var s = chordName.Trim();
        if (s.Length == 0) return 0;

        string rootStr;
        if (s.Length > 1 && (s[1] == '#' || s[1] == 'b'))
            rootStr = s.Substring(0, 2);
        else
            rootStr = s.Substring(0, 1);

        return rootStr switch
        {
            "C" => 0, "C#" => 1, "Db" => 1,
            "D" => 2, "D#" => 3, "Eb" => 3,
            "E" => 4,
            "F" => 5, "F#" => 6, "Gb" => 6,
            "G" => 7, "G#" => 8, "Ab" => 8,
            "A" => 9, "A#" => 10, "Bb" => 10,
            "B" => 11,
            _ => 0
        };
    }
}
