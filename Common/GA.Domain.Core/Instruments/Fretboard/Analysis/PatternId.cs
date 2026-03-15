namespace GA.Domain.Core.Instruments.Fretboard.Analysis;

using System.Collections.Immutable;

/// <summary>
///     Translation-invariant chord pattern identifier.
///     Two chord shapes that differ only by position on the neck share the same PatternId.
///     Muted strings are represented as -1, open strings as 0, fretted notes as relative fret offsets.
/// </summary>
public readonly record struct PatternId : IEquatable<PatternId>
{
    private readonly ImmutableArray<int> _relativeFrets;

    public PatternId(ImmutableArray<int> relativeFrets) => _relativeFrets = relativeFrets;

    public ImmutableArray<int> RelativeFrets => _relativeFrets.IsDefault ? [] : _relativeFrets;

    /// <summary>
    ///     Creates a PatternId from absolute fret positions by normalizing to relative offsets.
    ///     -1 = muted, 0 = open, positive = fretted.
    /// </summary>
    public static PatternId FromAbsoluteFrets(int[] frets)
    {
        // Find minimum non-muted fret (including open strings = fret 0)
        var minFret = int.MaxValue;
        foreach (var fret in frets)
        {
            if (fret >= 0 && fret < minFret) minFret = fret;
        }

        if (minFret == int.MaxValue) minFret = 0;

        // Normalize: subtract base fret from all non-muted positions
        var relative = new int[frets.Length];
        for (var i = 0; i < frets.Length; i++)
        {
            relative[i] = frets[i] < 0
                ? -1       // Muted
                : frets[i] - minFret; // Relative offset (open strings included)
        }

        return new([..relative]);
    }

    /// <summary>
    ///     Produces a human-readable pattern string (e.g., "x-0-2-2-1-0")
    /// </summary>
    public string ToPatternString() =>
        string.Join("-", RelativeFrets.Select(f => f < 0 ? "x" : f.ToString()));

    public bool Equals(PatternId other) => RelativeFrets.SequenceEqual(other.RelativeFrets);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var f in RelativeFrets) hash.Add(f);
        return hash.ToHashCode();
    }

    public override string ToString() => ToPatternString();
}
