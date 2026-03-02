namespace GA.Domain.Services.Fretboard.Shapes.Spectral;

using System.Collections.Immutable;

/// <summary>
///     Represents a cluster of fretboard shapes with shared spectral properties
/// </summary>
public record ShapeFamily
{
    /// <summary>
    ///     Unique cluster identifier
    /// </summary>
    public int ClusterId { get; init; }

    /// <summary>
    ///     IDs of shapes belonging to this family
    /// </summary>
    public required ImmutableList<string> ShapeIds { get; init; }

    /// <summary>
    ///     Number of shapes in the family
    /// </summary>
    public int Size => ShapeIds.Count;

    /// <summary>
    ///     Average ergonomics score for shapes in this family
    /// </summary>
    public double AverageErgonomics { get; init; }
}
