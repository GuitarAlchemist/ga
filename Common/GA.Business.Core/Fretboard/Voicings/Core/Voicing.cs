namespace GA.Business.Core.Fretboard.Voicings.Core;

using Primitives;
using GA.Business.Core.Notes.Primitives;

/// <summary>
/// Represents a guitar voicing - a specific way to play notes on the fretboard.
/// Equality is based on the position diagram (e.g., "0-0-x-x-x-x").
/// </summary>
/// <param name="Positions">The positions on each string (played or muted)</param>
/// <param name="Notes">The MIDI notes that are played</param>
public sealed record Voicing(Position[] Positions, MidiNote[] Notes)
{
    /// <summary>
    /// Gets the position diagram for this voicing (e.g., "0-0-x-x-x-x")
    /// </summary>
    private string PositionDiagram => VoicingExtensions.GetPositionDiagram(Positions);

    /// <summary>
    /// Equality is based on the position diagram, not the array references
    /// </summary>
    public bool Equals(Voicing? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        // Compare by position diagram for semantic equality
        return PositionDiagram == other.PositionDiagram;
    }

    /// <summary>
    /// Hash code based on position diagram for consistent hashing
    /// </summary>
    public override int GetHashCode()
    {
        return PositionDiagram.GetHashCode();
    }
}

