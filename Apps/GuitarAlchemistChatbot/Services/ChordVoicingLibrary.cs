namespace GuitarAlchemistChatbot.Services;

/// <summary>
///     Library of common chord voicings for guitar
/// </summary>
public static class ChordVoicingLibrary
{
    /// <summary>
    ///     Gets all available chord voicings
    /// </summary>
    public static Dictionary<string, List<ChordVoicing>> GetAllVoicings()
    {
        return new Dictionary<string, List<ChordVoicing>>
        {
            // Major Chords
            ["C"] =
            [
                new("C Major", "Open Position", [-1, 3, 2, 0, 1, 0], [0, 3, 2, 0, 1, 0], "C E G C E"),
                new("C Major", "Barre (8th fret)", [8, 10, 10, 9, 8, 8], [1, 3, 4, 2, 1, 1], "C G C E G C",
                    8, new(8, 0, 5))
            ],
            ["D"] =
            [
                new("D Major", "Open Position", [-1, -1, 0, 2, 3, 2], [0, 0, 0, 1, 3, 2], "D A D F#"),
                new("D Major", "Barre (10th fret)", [10, 12, 12, 11, 10, 10], [1, 3, 4, 2, 1, 1],
                    "D A D F# A D", 10, new(10, 0, 5))
            ],
            ["E"] =
            [
                new("E Major", "Open Position", [0, 2, 2, 1, 0, 0], [0, 2, 3, 1, 0, 0], "E B E G# B E"),
                new("E Major", "Barre (12th fret)", [12, 14, 14, 13, 12, 12], [1, 3, 4, 2, 1, 1],
                    "E B E G# B E", 12, new(12, 0, 5))
            ],
            ["F"] =
            [
                new("F Major", "Barre (1st fret)", [1, 3, 3, 2, 1, 1], [1, 3, 4, 2, 1, 1], "F C F A C F", 1,
                    new(1, 0, 5)),
                new("F Major", "Partial Barre", [-1, -1, 3, 2, 1, 1], [0, 0, 3, 2, 1, 1], "F A C F")
            ],
            ["G"] =
            [
                new("G Major", "Open Position", [3, 2, 0, 0, 0, 3], [3, 2, 0, 0, 0, 4], "G B D G B G"),
                new("G Major", "Barre (3rd fret)", [3, 5, 5, 4, 3, 3], [1, 3, 4, 2, 1, 1], "G D G B D G", 3,
                    new(3, 0, 5))
            ],
            ["A"] =
            [
                new("A Major", "Open Position", [-1, 0, 2, 2, 2, 0], [0, 0, 2, 3, 4, 0], "A E A C# E"),
                new("A Major", "Barre (5th fret)", [5, 7, 7, 6, 5, 5], [1, 3, 4, 2, 1, 1], "A E A C# E A",
                    5, new(5, 0, 5))
            ],
            ["B"] =
            [
                new("B Major", "Barre (7th fret)", [7, 9, 9, 8, 7, 7], [1, 3, 4, 2, 1, 1], "B F# B D# F# B",
                    7, new(7, 0, 5)),
                new("B Major", "Open Position", [-1, 2, 4, 4, 4, 2], [0, 1, 3, 3, 3, 1], "B F# B D# F#")
            ],

            // Minor Chords
            ["Am"] =
            [
                new("A Minor", "Open Position", [-1, 0, 2, 2, 1, 0], [0, 0, 2, 3, 1, 0], "A E A C E"),
                new("A Minor", "Barre (5th fret)", [5, 7, 7, 5, 5, 5], [1, 3, 4, 1, 1, 1], "A E A C E A", 5,
                    new(5, 0, 5))
            ],
            ["Dm"] =
            [
                new("D Minor", "Open Position", [-1, -1, 0, 2, 3, 1], [0, 0, 0, 2, 3, 1], "D A D F"),
                new("D Minor", "Barre (10th fret)", [10, 12, 12, 10, 10, 10], [1, 3, 4, 1, 1, 1],
                    "D A D F A D", 10, new(10, 0, 5))
            ],
            ["Em"] =
            [
                new("E Minor", "Open Position", [0, 2, 2, 0, 0, 0], [0, 2, 3, 0, 0, 0], "E B E G B E"),
                new("E Minor", "Barre (12th fret)", [12, 14, 14, 12, 12, 12], [1, 3, 4, 1, 1, 1],
                    "E B E G B E", 12, new(12, 0, 5))
            ],

            // Seventh Chords
            ["Cmaj7"] =
            [
                new("C Major 7", "Open Position", [-1, 3, 2, 0, 0, 0], [0, 3, 2, 0, 0, 0], "C E G B"),
                new("C Major 7", "8th fret", [8, 10, 9, 9, 8, 8], [1, 3, 2, 2, 1, 1], "C G B E G C", 8)
            ],
            ["Dm7"] =
            [
                new("D Minor 7", "Open Position", [-1, -1, 0, 2, 1, 1], [0, 0, 0, 2, 1, 1], "D A C F"),
                new("D Minor 7", "10th fret", [10, 12, 10, 10, 10, 10], [1, 3, 1, 1, 1, 1], "D A C F A D",
                    10, new(10, 0, 5))
            ],
            ["Em7"] =
            [
                new("E Minor 7", "Open Position", [0, 2, 0, 0, 0, 0], [0, 2, 0, 0, 0, 0], "E B D G B E"),
                new("E Minor 7", "12th fret", [12, 14, 12, 12, 12, 12], [1, 3, 1, 1, 1, 1], "E B D G B E",
                    12, new(12, 0, 5))
            ],
            ["G7"] =
            [
                new("G Dominant 7", "Open Position", [3, 2, 0, 0, 0, 1], [3, 2, 0, 0, 0, 1], "G B D G B F"),
                new("G Dominant 7", "3rd fret", [3, 5, 3, 4, 3, 3], [1, 3, 1, 2, 1, 1], "G D F B D G", 3,
                    new(3, 0, 5))
            ],
            ["C7"] =
            [
                new("C Dominant 7", "Open Position", [-1, 3, 2, 3, 1, 0], [0, 3, 2, 4, 1, 0], "C E Bb C E"),
                new("C Dominant 7", "8th fret", [8, 10, 8, 9, 8, 8], [1, 3, 1, 2, 1, 1], "C G Bb E G C", 8,
                    new(8, 0, 5))
            ],
            ["F7"] =
            [
                new("F Dominant 7", "1st fret", [1, 3, 1, 2, 1, 1], [1, 3, 1, 2, 1, 1], "F C Eb A C F", 1,
                    new(1, 0, 5))
            ],
            ["Fmaj7"] =
            [
                new("F Major 7", "1st fret", [1, 3, 2, 2, 1, 1], [1, 3, 2, 2, 1, 1], "F C E A C F", 1,
                    new(1, 0, 5)),
                new("F Major 7", "Open Position", [-1, -1, 3, 2, 1, 0], [0, 0, 3, 2, 1, 0], "F A C E")
            ],
            ["Amaj7"] =
            [
                new("A Major 7", "Open Position", [-1, 0, 2, 1, 2, 0], [0, 0, 2, 1, 3, 0], "A E G# C# E"),
                new("A Major 7", "5th fret", [5, 7, 6, 6, 5, 5], [1, 3, 2, 2, 1, 1], "A E G# C# E A", 5)
            ],

            // Extended Chords
            ["Cmaj9"] =
            [
                new("C Major 9", "Open Position", [-1, 3, 0, 0, 0, 0], [0, 3, 0, 0, 0, 0], "C G B D G")
            ],
            ["Dm9"] =
            [
                new("D Minor 9", "Open Position", [-1, -1, 0, 2, 1, 0], [0, 0, 0, 2, 1, 0], "D A C E")
            ],

            // Diminished and Augmented
            ["Bdim"] =
            [
                new("B Diminished", "Open Position", [-1, 2, 3, 4, 3, -1], [0, 1, 2, 4, 3, 0], "B D F")
            ],
            ["Caug"] =
            [
                new("C Augmented", "Open Position", [-1, 3, 2, 1, 1, 0], [0, 4, 3, 2, 1, 0], "C E G#")
            ]
        };
    }

    /// <summary>
    ///     Gets voicings for a specific chord
    /// </summary>
    public static List<ChordVoicing>? GetVoicings(string chordName)
    {
        var voicings = GetAllVoicings();

        // Try exact match first
        if (voicings.TryGetValue(chordName, out var exactMatch))
        {
            return exactMatch;
        }

        // Try case-insensitive match
        var key = voicings.Keys.FirstOrDefault(k =>
            k.Equals(chordName, StringComparison.OrdinalIgnoreCase));

        return key != null ? voicings[key] : null;
    }

    /// <summary>
    ///     Searches for chord voicings by partial name
    /// </summary>
    public static Dictionary<string, List<ChordVoicing>> SearchVoicings(string query)
    {
        var allVoicings = GetAllVoicings();
        var queryLower = query.ToLower();

        return allVoicings
            .Where(kvp => kvp.Key.ToLower().Contains(queryLower) ||
                          kvp.Value.Any(v => v.FullName.ToLower().Contains(queryLower)))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    ///     Gets all available chord names
    /// </summary>
    public static IEnumerable<string> GetAllChordNames()
    {
        return GetAllVoicings().Keys.OrderBy(k => k);
    }
}

/// <summary>
///     Represents a chord voicing on guitar
/// </summary>
public record ChordVoicing(
    string FullName,
    string Position,
    int[] Frets,
    int[] Fingers,
    string Notes,
    int StartFret = 0,
    BarreInfo? Barre = null)
{
    /// <summary>
    ///     Gets a description of the voicing
    /// </summary>
    public string GetDescription()
    {
        var desc = $"{FullName} - {Position}";
        if (Barre != null)
        {
            desc += " (Barre)";
        }

        return desc;
    }
}

/// <summary>
///     Information about a barre in a chord
/// </summary>
public record BarreInfo(int Fret, int FromString, int ToStringNum);
