namespace GA.Business.Core.Fretboard.Invariants;

using System.Collections.Immutable;

[PublicAPI]
public sealed class ChordPatternAnalysisResult(
    int totalChords,
    int uniquePatterns,
    double compressionRatio,
    double averageTranspositions,
    ImmutableDictionary<PatternId, ImmutableList<ChordInvariant>> groups)
{
    public int TotalChords { get; } = totalChords;
    public int UniquePatterns { get; } = uniquePatterns;
    public double CompressionRatio { get; } = compressionRatio;
    public double AverageTranspositions { get; } = averageTranspositions;
    public ImmutableDictionary<PatternId, ImmutableList<ChordInvariant>> Groups { get; } = groups;

    public override string ToString()
    {
        var percent = (CompressionRatio * 100.0).ToString("0.00");
        return $"Total: {TotalChords}, Unique: {UniquePatterns}, Compression: {percent}%, AvgTranspositions: {AverageTranspositions:0.0}";
    }
}
