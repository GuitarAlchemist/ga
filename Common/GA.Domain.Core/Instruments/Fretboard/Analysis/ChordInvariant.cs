namespace GA.Domain.Core.Instruments.Fretboard.Analysis;

/// <summary>
///     Translation-invariant representation of a chord voicing.
///     Captures the chord shape (<see cref="PatternId"/>) and its position on the neck (<see cref="BaseFret"/>).
/// </summary>
public readonly record struct ChordInvariant(PatternId PatternId, int BaseFret)
{
    /// <summary>
    ///     Creates a ChordInvariant from absolute fret values and tuning.
    ///     -1 indicates a muted string; 0 indicates an open string.
    /// </summary>
    public static ChordInvariant FromFrets(int[] frets, Tuning tuning)
    {
        // Find the base fret (minimum non-muted fret, including open)
        var baseFret = int.MaxValue;
        foreach (var fret in frets)
        {
            if (fret >= 0 && fret < baseFret) baseFret = fret;
        }

        if (baseFret == int.MaxValue) baseFret = 0;

        var patternId = PatternId.FromAbsoluteFrets(frets);
        return new(patternId, baseFret);
    }
}
