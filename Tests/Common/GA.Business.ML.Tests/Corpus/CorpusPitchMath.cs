namespace GA.Business.ML.Tests.Corpus;

/// <summary>
///     Pitch-class and fret arithmetic used to check that the corpus is
///     self-consistent.
/// </summary>
/// <remarks>
///     <para>
///         Deliberately independent of production code. #627 forbids duplicating
///         key detection or harmonic analysis in the harness, and none of that
///         happens here: this is note-name parsing, an interval table, and
///         "open string + fret = sounding pitch". No key is inferred, no chord
///         is analysed, no function is assigned.
///     </para>
///     <para>
///         Independence is the point. If these helpers called
///         <c>ChordVocabulary</c> or <c>KeyIdentificationService</c>, a defect in
///         production would make wrong corpus data validate clean - exactly the
///         circularity a held-out corpus exists to avoid.
///     </para>
/// </remarks>
internal static class CorpusPitchMath
{
    private static readonly IReadOnlyDictionary<char, int> LetterPitchClass = new Dictionary<char, int>
    {
        ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5, ['G'] = 7, ['A'] = 9, ['B'] = 11
    };

    /// <summary>Semitones above the root for each corpus chord quality.</summary>
    public static readonly IReadOnlyDictionary<string, int[]> QualityIntervals = new Dictionary<string, int[]>
    {
        ["major-triad"]       = [0, 4, 7],
        ["minor-triad"]       = [0, 3, 7],
        ["dominant-7"]        = [0, 4, 7, 10],
        ["major-7"]           = [0, 4, 7, 11],
        ["minor-7"]           = [0, 3, 7, 10],
        ["half-diminished-7"] = [0, 3, 6, 10]
    };

    /// <summary>Diatonic letter step for each chord tone, parallel to <see cref="QualityIntervals" />.</summary>
    public static readonly IReadOnlyDictionary<string, int[]> QualityLetterSteps = new Dictionary<string, int[]>
    {
        ["major-triad"]       = [0, 2, 4],
        ["minor-triad"]       = [0, 2, 4],
        ["dominant-7"]        = [0, 2, 4, 6],
        ["major-7"]           = [0, 2, 4, 6],
        ["minor-7"]           = [0, 2, 4, 6],
        ["half-diminished-7"] = [0, 2, 4, 6]
    };

    /// <summary>Pitch class of a bare note name such as <c>C</c>, <c>Eb</c>, <c>F#</c>, <c>Bbb</c>.</summary>
    public static int PitchClassOf(string noteName)
    {
        if (noteName.Length == 0 || !LetterPitchClass.TryGetValue(noteName[0], out var pc))
            throw new FormatException($"'{noteName}' is not a note name");

        foreach (var accidental in noteName.AsSpan(1))
        {
            pc += accidental switch
            {
                '#' => 1,
                'b' => -1,
                _ => throw new FormatException($"'{noteName}' has an unexpected accidental '{accidental}'")
            };
        }

        return ((pc % 12) + 12) % 12;
    }

    /// <summary>MIDI number of a scientific-pitch name such as <c>E2</c> or <c>F#3</c>.</summary>
    public static int MidiOf(string scientificPitch)
    {
        var octave = scientificPitch[^1] - '0';
        if (octave is < 0 or > 9) throw new FormatException($"'{scientificPitch}' has no octave digit");

        return ((octave + 1) * 12) + PitchClassOf(scientificPitch[..^1]);
    }

    /// <summary>Spells the chord tone <paramref name="semitones" /> above <paramref name="root" />.</summary>
    /// <remarks>
    ///     The letter step is what forces <c>Eb</c> rather than <c>D#</c> as the
    ///     minor third of C. Same rule as the production
    ///     <c>ChordSpelling.Spell</c>, re-derived here rather than called, for the
    ///     independence reason in the type remarks.
    /// </remarks>
    public static string Spell(string root, int semitones, int letterSteps)
    {
        const string letters = "CDEFGAB";

        var rootIndex = letters.IndexOf(root[0]);
        if (rootIndex < 0) throw new FormatException($"'{root}' is not a note name");

        var targetLetter = letters[(rootIndex + letterSteps) % 7];
        var targetPc = (PitchClassOf(root) + semitones) % 12;
        var delta = ((targetPc - LetterPitchClass[targetLetter]) % 12 + 12) % 12;

        var accidental = delta switch
        {
            0 => "",
            1 => "#",
            2 => "##",
            10 => "bb",
            11 => "b",
            _ => throw new InvalidOperationException(
                $"{root} + {semitones} at letter step {letterSteps} needs {delta} accidentals")
        };

        return targetLetter + accidental;
    }

    /// <summary>Pitch classes actually sounded by a fretting, given the open strings.</summary>
    public static IReadOnlySet<int> SoundedPitchClasses(
        IReadOnlyList<string> openStrings,
        IReadOnlyList<int?> fretsLowToHigh)
    {
        if (openStrings.Count != fretsLowToHigh.Count)
        {
            throw new ArgumentException(
                $"{fretsLowToHigh.Count} frets for {openStrings.Count} strings", nameof(fretsLowToHigh));
        }

        var sounded = new HashSet<int>();
        for (var i = 0; i < openStrings.Count; i++)
            if (fretsLowToHigh[i] is { } fret)
                sounded.Add((MidiOf(openStrings[i]) + fret) % 12);

        return sounded;
    }
}
