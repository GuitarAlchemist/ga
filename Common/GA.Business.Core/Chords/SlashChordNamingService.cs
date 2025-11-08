namespace GA.Business.Core.Chords;

using Atonal;

/// <summary>
///     Provides simple slash-chord analysis and naming utilities.
/// </summary>
public static class SlashChordNamingService
{
    public enum SlashChordType
    {
        Inversion,
        NonDiatonicBass,
        PedalBass,
        Other
    }

    /// <summary>
    ///     Determines if a slash chord (chord over bass) is valid. Minimal check: bass differs from root.
    /// </summary>
    public static bool IsValidSlashChord(ChordTemplate template, PitchClass root, PitchClass bass)
    {
        return root.Value != bass.Value;
    }

    /// <summary>
    ///     Analyzes a slash chord and returns basic information including type and notation.
    /// </summary>
    public static SlashChordAnalysis AnalyzeSlashChord(ChordTemplate template, PitchClass root, PitchClass bass)
    {
        var diff = (bass.Value - root.Value + 12) % 12;
        // Treat common chord tones (3rd, 5th) as inversions; allow both minor/major thirds
        var isInversion = diff is 3 or 4 or 7;
        var isCommon = isInversion;
        var type = isInversion ? SlashChordType.Inversion : SlashChordType.Other;
        var notation = $"{ToNoteName(root)}/{ToNoteName(bass)}";
        return new SlashChordAnalysis(type, notation, isCommon);
    }

    /// <summary>
    ///     Generates candidate slash chord names. Minimal implementation returns root/bass.
    /// </summary>
    public static IEnumerable<string> GenerateSlashChordNames(ChordTemplate template, PitchClass root, PitchClass bass)
    {
        yield return $"{ToNoteName(root)}/{ToNoteName(bass)}";
    }

    private static string ToNoteName(PitchClass pitchClass)
    {
        return pitchClass.Value switch
        {
            0 => "C",
            1 => "C#",
            2 => "D",
            3 => "D#",
            4 => "E",
            5 => "F",
            6 => "F#",
            7 => "G",
            8 => "G#",
            9 => "A",
            10 => "A#",
            11 => "B",
            _ => "?"
        };
    }

    public record SlashChordAnalysis(SlashChordType Type, string SlashNotation, bool IsCommonInversion);
}
