namespace GA.Business.Core.Context;

using System.Collections.Immutable;
using JetBrains.Annotations;
using GA.Domain.Core.Design.Attributes;

/// <summary>
/// Defines a fretboard range constraint for voicing searches and visualizations
/// </summary>
[PublicAPI]
[DomainInvariant("Minimum fret must be less than or equal to maximum fret", "MinFret <= MaxFret")]
[DomainInvariant("Fret numbers must be non-negative", "MinFret >= 0 && MaxFret >= 0")]
public sealed record FretboardRange
{
    /// <summary>
    /// Minimum fret number (inclusive)
    /// </summary>
    public required int MinFret { get; init; }

    /// <summary>
    /// Maximum fret number (inclusive)
    /// </summary>
    public required int MaxFret { get; init; }

    /// <summary>
    /// Strings that are available (1-based indexing, e.g., {1,2,3,4,5,6} for all strings)
    /// </summary>
    public ImmutableHashSet<int> AvailableStrings { get; init; } = [];

    /// <summary>
    /// Creates a fretboard range for all strings
    /// </summary>
    public static FretboardRange Create(int minFret, int maxFret, int stringCount) =>
        new()
        {
            MinFret = minFret,
            MaxFret = maxFret,
            AvailableStrings = [.. Enumerable.Range(1, stringCount)]
        };

    /// <summary>
    /// Creates an open position range (frets 0-3, all strings)
    /// </summary>
    public static FretboardRange OpenPosition(int stringCount = 6) =>
        Create(0, 3, stringCount);

    /// <summary>
    /// Creates a full neck range
    /// </summary>
    public static FretboardRange FullNeck(int stringCount = 6, int fretCount = 24) =>
        Create(0, fretCount, stringCount);

    /// <summary>
    /// Checks if a fret/string combination is within this range
    /// </summary>
    public bool Contains(int fret, int @string) =>
        fret >= MinFret && fret <= MaxFret && AvailableStrings.Contains(@string);

    /// <summary>
    /// Gets the span of frets in this range
    /// </summary>
    public int Span => MaxFret - MinFret + 1;
}
