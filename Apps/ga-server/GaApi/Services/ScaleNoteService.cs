namespace GaApi.Services;

/// <summary>
/// Computes the 7 pitch-class names for a given key string (e.g. "G major", "A minor").
/// Used to emit <c>ga:scale</c> AG-UI CUSTOM events for the live fretboard scale overlay.
/// </summary>
public static class ScaleNoteService
{
    // Semitone offsets from root for major and natural minor scales
    private static readonly int[] Major = [0, 2, 4, 5, 7, 9, 11];
    private static readonly int[] Minor = [0, 2, 3, 5, 7, 8, 10];

    // Canonical flat-key spelling preferred; sharps for common sharp keys
    private static readonly string[] NoteNames =
        ["C", "Db", "D", "Eb", "E", "F", "F#", "G", "Ab", "A", "Bb", "B"];

    private static readonly Dictionary<string, int> RootPc =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["C"] = 0,  ["C#"] = 1, ["Db"] = 1,
            ["D"] = 2,  ["D#"] = 3, ["Eb"] = 3,
            ["E"] = 4,  ["F"]  = 5, ["F#"] = 6,
            ["Gb"] = 6, ["G"]  = 7, ["G#"] = 8,
            ["Ab"] = 8, ["A"]  = 9, ["A#"] = 10, ["Bb"] = 10,
            ["B"] = 11,
        };

    /// <summary>
    /// Returns 7 scale degree descriptors for the given key string,
    /// or <see langword="null"/> if the key string cannot be parsed.
    /// </summary>
    public static ScaleNote[]? GetNotes(string key)
    {
        var parts = key.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1) return null;

        var rootStr  = parts[0];
        var modeStr  = parts.Length >= 2 ? parts[1] : "major";
        var isMinor  = modeStr.Equals("minor", StringComparison.OrdinalIgnoreCase);

        if (!RootPc.TryGetValue(rootStr, out var rootPc)) return null;

        var offsets = isMinor ? Minor : Major;

        return offsets
            .Select((offset, i) => new ScaleNote(
                Degree: i + 1,
                Note:   NoteNames[(rootPc + offset) % 12],
                PitchClass: (rootPc + offset) % 12))
            .ToArray();
    }
}

/// <summary>Scale degree descriptor emitted in <c>ga:scale</c> events.</summary>
public sealed record ScaleNote(int Degree, string Note, int PitchClass);
