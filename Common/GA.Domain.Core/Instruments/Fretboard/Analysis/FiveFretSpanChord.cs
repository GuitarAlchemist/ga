namespace GA.Domain.Core.Instruments.Fretboard.Analysis;

using System.Collections.Immutable;
using Primitives;

/// <summary>
///     A chord voicing within a 5-fret span window on the fretboard.
/// </summary>
public sealed record FiveFretSpanChord(
    ImmutableList<Position> Positions,
    ChordInvariant Invariant,
    int LowestFret,
    int HighestFret,
    string ChordName);
