namespace GA.Domain.Core.Theory.Harmony;

/// <summary>
///     Provides common chord formulas
/// </summary>
public static class CommonChordFormulas
{
    /// <summary>
    ///     Major triad (1, 3, 5)
    /// </summary>
    public static ChordFormula Major => FromSemitones("Major", 4, 7);

    /// <summary>
    ///     Minor triad (1, b3, 5)
    /// </summary>
    public static ChordFormula Minor => FromSemitones("Minor", 3, 7);

    /// <summary>
    ///     Diminished triad (1, b3, b5)
    /// </summary>
    public static ChordFormula Diminished => FromSemitones("Diminished", 3, 6);

    /// <summary>
    ///     Augmented triad (1, 3, #5)
    /// </summary>
    public static ChordFormula Augmented => FromSemitones("Augmented", 4, 8);

    /// <summary>
    ///     Major seventh (1, 3, 5, 7)
    /// </summary>
    public static ChordFormula Major7 => FromSemitones("Major 7th", 4, 7, 11);

    /// <summary>
    ///     Minor seventh (1, b3, 5, b7)
    /// </summary>
    public static ChordFormula Minor7 => FromSemitones("Minor 7th", 3, 7, 10);

    /// <summary>
    ///     Dominant seventh (1, 3, 5, b7)
    /// </summary>
    public static ChordFormula Dominant7 => FromSemitones("Dominant 7th", 4, 7, 10);

    private static ChordFormula FromSemitones(string name, params int[] semitones)
    {
        return ChordFormula.FromSemitones(name, semitones);
    }
}