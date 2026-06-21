namespace GA.Domain.Core.Instruments.Fretboard.Voicings.Core;

using System.Linq;
using Domain.Core.Primitives.Notes;
using Primitives;

/// <summary>
///     Represents a guitar voicing - a specific way to play notes on the fretboard.
///     Equality is based on the position diagram (e.g., "0-0-x-x-x-x").
///     See <see href="https://en.wikipedia.org/wiki/Voicing_(music)" />.
/// </summary>
/// <param name="Positions">The positions on each string (played or muted)</param>
/// <param name="Notes">The MIDI notes that are played</param>
public sealed record Voicing(Position[] Positions, MidiNote[] Notes)
{
    /// <summary>
    ///     Position diagram for this voicing (e.g., "0-0-x-x-x-x"). Voicing equality is based on this.
    /// </summary>
    public string Diagram
    {
        get
        {
            var parts = new string[Positions.Length];
            for (var i = 0; i < Positions.Length; i++)
            {
                parts[i] = Positions[i] switch
                {
                    Position.Muted => "x",
                    Position.Played played => played.Location.Fret.Value.ToString(),
                    _ => "x"
                };
            }

            return string.Join("-", parts);
        }
    }

    /// <summary>Fret span across fretted notes (0 if none).</summary>
    public int FretSpan
    {
        get
        {
            var fretted = Positions.OfType<Position.Played>().Where(p => p.Location.Fret.Value > 0).ToList();
            return fretted.Count == 0
                ? 0
                : fretted.Max(p => p.Location.Fret.Value) - fretted.Min(p => p.Location.Fret.Value);
        }
    }

    /// <summary>Lowest fretted fret, or null if no fretted notes.</summary>
    public int? MinFret
    {
        get
        {
            var fretted = Positions.OfType<Position.Played>().Where(p => p.Location.Fret.Value > 0).ToList();
            return fretted.Count > 0 ? fretted.Min(p => p.Location.Fret.Value) : null;
        }
    }

    /// <summary>Highest played fret, or null if no played notes.</summary>
    public int? MaxFret
    {
        get
        {
            var played = Positions.OfType<Position.Played>().ToList();
            return played.Count > 0 ? played.Max(p => p.Location.Fret.Value) : null;
        }
    }

    /// <summary>Number of played (non-muted) notes.</summary>
    public int PlayedNoteCount => Positions.OfType<Position.Played>().Count();

    /// <summary>
    ///     True if 3+ notes share a fret (grouping semantics, not adjacency).
    ///     NOTE: a separate adjacency-aware barre check lives in
    ///     VoicingPhysicalAnalyzer.DetectBarreRequirement; unifying the two is a
    ///     deliberate follow-up (changes indexed BarreRequired — see PR #456).
    /// </summary>
    public bool HasBarre() =>
        Positions.OfType<Position.Played>().GroupBy(p => p.Location.Fret.Value).Any(g => g.Count() >= 3);

    /// <summary>
    ///     Equality is based on the position diagram, not the array references
    /// </summary>
    public bool Equals(Voicing? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        // Compare by position diagram for semantic equality
        return Diagram == other.Diagram;
    }

    /// <summary>
    ///     Hash code based on position diagram for consistent hashing
    /// </summary>
    public override int GetHashCode() => Diagram.GetHashCode();
}
