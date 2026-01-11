namespace GA.Business.Core.Fretboard.Shapes.Spectral;

using System.Collections.Generic;

/// <summary>
/// A family of related chords discovered through spectral clustering
/// </summary>
public record ChordFamily
{
    public required int Id { get; init; }
    public required List<string> ShapeIds { get; init; }
    public required string Centroid { get; init; }
    public required int Size { get; init; }
    public required double AverageErgonomics { get; init; }
}

