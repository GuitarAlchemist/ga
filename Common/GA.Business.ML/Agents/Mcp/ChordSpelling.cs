namespace GA.Business.ML.Agents.Mcp;

/// <summary>
/// Enharmonic-aware chord-tone spelling shared between
/// <see cref="Skills.ChordInfoSkill"/> (the deterministic fast path) and
/// <see cref="ChordMcpTools"/> (the MCP-tool surface). Both call
/// <see cref="Spell"/> with the same arguments and previously held byte-
/// identical copies — drift between them would have caused the same chord
/// query to return different note spellings depending on which path
/// resolved it. Extracted per PR #80 review and shipped in PR #102.
/// </summary>
/// <remarks>
/// The function picks the right letter+accidental combination so a Cmaj
/// chord spells C-E-G (not C-E-Abb) and a Bbm chord spells Bb-Db-F
/// (not Bb-C#-F). The choice is driven by <c>letterSteps</c> from the
/// chord-formula table (e.g. triad steps [0, 2, 4]; seventh-chord steps
/// [0, 2, 4, 6]) — the caller picks letterSteps based on the chord
/// quality and this function does the enharmonic accounting.
/// </remarks>
internal static class ChordSpelling
{
    /// <summary>
    /// Diatonic letters in C-major order. Used as a 7-position cycle so
    /// "letter step N from C" = NaturalLetters[(0+N) % 7].
    /// </summary>
    public static readonly char[] NaturalLetters = ['C', 'D', 'E', 'F', 'G', 'A', 'B'];

    /// <summary>
    /// Pitch class of each natural letter (no accidentals). Used to compute
    /// the accidental needed to reach the target pitch class from the
    /// chosen letter.
    /// </summary>
    public static readonly IReadOnlyDictionary<char, int> NaturalPitchClasses =
        new Dictionary<char, int>
        {
            ['C'] = 0, ['D'] = 2, ['E'] = 4, ['F'] = 5,
            ['G'] = 7, ['A'] = 9, ['B'] = 11,
        };

    /// <summary>
    /// Returns the enharmonic-aware spelling of a chord tone given its
    /// <paramref name="root"/>, target <paramref name="pitchClass"/>, and
    /// the number of <paramref name="letterSteps"/> from root to target
    /// (per the chord formula).
    /// </summary>
    /// <example>
    /// <c>Spell("C", 4, 2)</c> = <c>"E"</c> (root C, third = letter step 2,
    /// pitch class 4 → letter E with no accidental).
    /// <c>Spell("C", 3, 2)</c> = <c>"Eb"</c> (root C, third = letter step 2,
    /// pitch class 3 → letter E flatted by one — the minor third of C).
    /// </example>
    public static string Spell(string root, int pitchClass, int letterSteps)
    {
        var rootLetter   = char.ToUpperInvariant(root[0]);
        var rootIndex    = Array.IndexOf(NaturalLetters, rootLetter);
        var targetLetter = NaturalLetters[(rootIndex + letterSteps) % NaturalLetters.Length];
        var targetNatural = NaturalPitchClasses[targetLetter];
        var normalized   = ((pitchClass % 12) + 12) % 12;
        var accidental   = ((normalized - targetNatural) % 12 + 12) % 12;

        return accidental switch
        {
            0  => targetLetter.ToString(),
            1  => $"{targetLetter}#",
            2  => $"{targetLetter}##",
            10 => $"{targetLetter}bb",
            11 => $"{targetLetter}b",
            _  => $"{targetLetter}{(accidental < 6 ? new string('#', accidental) : new string('b', 12 - accidental))}",
        };
    }
}
