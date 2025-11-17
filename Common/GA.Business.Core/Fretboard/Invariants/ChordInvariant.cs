namespace GA.Business.Core.Fretboard.Invariants;

using Atonal;
using Fretboard;
using GA.Business.Core.Notes.Primitives;

/// <summary>
/// Normalized representation of a chord voicing on a fretted instrument.
/// </summary>
[PublicAPI]
public sealed class ChordInvariant
{
    private ChordInvariant(int baseFret, PatternId patternId, PitchClassSet pitchClassSet)
    {
        BaseFret = baseFret;
        PatternId = patternId;
        PitchClassSet = pitchClassSet;
    }

    public int BaseFret { get; }
    public PatternId PatternId { get; }
    public PitchClassSet PitchClassSet { get; }

    /// <summary>
    /// Creates a chord invariant from absolute fret numbers and a tuning.
    /// </summary>
    public static ChordInvariant FromFrets(int[] frets, Tuning tuning)
    {
        ArgumentNullException.ThrowIfNull(frets);
        ArgumentNullException.ThrowIfNull(tuning);
        if (frets.Length != 6)
        {
            throw new ArgumentException("Expected 6 fret values", nameof(frets));
        }

        // Determine base fret (min of fretted values, ignore -1)
        var baseFret = GetBaseFret(frets);

        // Build normalized pattern relative to base fret
        var pattern = PatternId.FromPattern(frets);

        // Compute pitch classes
        var pcs = CalculatePitchClassSet(frets, tuning);

        return new ChordInvariant(baseFret, pattern, pcs);
    }

    public bool IsSamePattern(ChordInvariant other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return PatternId == other.PatternId;
    }

    public bool IsSameChord(ChordInvariant other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return PitchClassSet == other.PitchClassSet;
    }

    /// <summary>
    /// Returns transpositions of the pattern across frets up to <paramref name="maxFret"/>.
    /// </summary>
    public IEnumerable<(int baseFret, int[] frets)> GetAllTranspositions(int maxFret)
    {
        var norm = PatternId.ToPattern();
        for (var k = 0; k <= maxFret; k++)
        {
            var arr = new int[norm.Length];
            for (var i = 0; i < norm.Length; i++)
            {
                var v = norm[i];
                arr[i] = v < 0 ? -1 : v + k;
            }

            yield return (k, arr);
        }
    }

    /// <summary>
    /// Converts the normalized pattern back to absolute frets given a target base fret.
    /// </summary>
    public int[] ToAbsoluteFrets(int targetBaseFret)
    {
        var norm = PatternId.ToPattern();
        var arr = new int[norm.Length];
        for (var i = 0; i < norm.Length; i++)
        {
            var v = norm[i];
            arr[i] = v < 0 ? -1 : v + targetBaseFret;
        }
        return arr;
    }

    public ChordDifficulty GetDifficulty()
    {
        var score = PatternId.GetComplexityScore();
        return score switch
        {
            <= 4 => ChordDifficulty.Beginner,
            <= 10 => ChordDifficulty.Intermediate,
            <= 18 => ChordDifficulty.Advanced,
            _ => ChordDifficulty.Expert
        };
    }

    public string GetPatternDescription()
    {
        var pattern = PatternId.ToPattern();
        var muted = pattern.Count(v => v < 0);
        var open = pattern.Count(v => v == 0);
        var fretted = pattern.Count(v => v > 0);
        return $"Pattern with {pattern.Length} strings: {muted} muted, {open} open, {fretted} fretted";
    }

    public override string ToString()
    {
        return $"BaseFret: {BaseFret}, Pattern: {PatternId.ToPatternString()}";
    }

    private static int GetBaseFret(int[] frets)
    {
        var min = int.MaxValue;
        foreach (var v in frets)
        {
            if (v >= 0 && v < min) min = v;
        }
        return min == int.MaxValue ? 0 : min;
    }

    private static PitchClassSet CalculatePitchClassSet(int[] frets, Tuning tuning)
    {
        var list = new List<PitchClass>(6);
        var pitches = tuning.AsSpan(); // highest string first
        var stringCount = tuning.StringCount;
        for (var i = 0; i < frets.Length && i < stringCount; i++)
        {
            var fret = frets[i];
            if (fret < 0) continue; // muted
            // Map array index (low E..high E) to tuning index (high..low)
            var tuningIndex = stringCount - 1 - i;
            var open = pitches[tuningIndex];
            MidiNote midi = open.MidiNote + fret;
            list.Add(midi.PitchClass);
        }

        return new PitchClassSet(list);
    }
}

public enum ChordDifficulty
{
    Beginner,
    Intermediate,
    Advanced,
    Expert
}
