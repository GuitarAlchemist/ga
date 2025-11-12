namespace GA.Business.Core.Fretboard.Voicings.Core;

using GA.Business.Core.Fretboard.Primitives;

/// <summary>
/// Extension methods for working with voicings
/// </summary>
public static class VoicingExtensions
{
    /// <summary>
    /// Gets a unique string representation of a voicing's positions (e.g., "0-0-x-x-x-x")
    /// </summary>
    /// <param name="positions">The positions to convert to a diagram</param>
    /// <returns>String diagram representation</returns>
    public static string GetPositionDiagram(Position[] positions)
    {
        var parts = new string[positions.Length];
        for (var i = 0; i < positions.Length; i++)
        {
            parts[i] = positions[i] switch
            {
                Position.Muted => "x",
                Position.Played played => played.Location.Fret.Value.ToString(),
                _ => "x"
            };
        }
        return string.Join("-", parts);
    }

    /// <summary>
    /// Gets the fret span of a voicing (excluding open strings)
    /// </summary>
    /// <param name="positions">The positions to analyze</param>
    /// <returns>The fret span, or 0 if no fretted notes</returns>
    public static int GetFretSpan(Position[] positions)
    {
        var frettedPositions = positions
            .OfType<Position.Played>()
            .Where(p => p.Location.Fret.Value > 0)
            .ToList();

        if (!frettedPositions.Any())
            return 0;

        var minFret = frettedPositions.Min(p => p.Location.Fret.Value);
        var maxFret = frettedPositions.Max(p => p.Location.Fret.Value);

        return maxFret - minFret;
    }

    /// <summary>
    /// Gets the number of played notes in a voicing
    /// </summary>
    /// <param name="positions">The positions to count</param>
    /// <returns>Number of played notes</returns>
    public static int GetPlayedNoteCount(Position[] positions)
    {
        return positions.OfType<Position.Played>().Count();
    }

    /// <summary>
    /// Checks if a voicing contains a barre (3 or more notes on the same fret)
    /// </summary>
    /// <param name="positions">The positions to check</param>
    /// <returns>True if the voicing contains a barre</returns>
    public static bool HasBarre(Position[] positions)
    {
        return positions
            .OfType<Position.Played>()
            .GroupBy(p => p.Location.Fret.Value)
            .Any(g => g.Count() >= 3);
    }

    /// <summary>
    /// Gets the minimum fret position (excluding open strings and muted strings)
    /// </summary>
    /// <param name="positions">The positions to analyze</param>
    /// <returns>Minimum fret, or null if no fretted notes</returns>
    public static int? GetMinFret(Position[] positions)
    {
        var frettedPositions = positions
            .OfType<Position.Played>()
            .Where(p => p.Location.Fret.Value > 0)
            .ToList();

        return frettedPositions.Any() 
            ? frettedPositions.Min(p => p.Location.Fret.Value) 
            : null;
    }

    /// <summary>
    /// Gets the maximum fret position
    /// </summary>
    /// <param name="positions">The positions to analyze</param>
    /// <returns>Maximum fret, or null if no played notes</returns>
    public static int? GetMaxFret(Position[] positions)
    {
        var playedPositions = positions.OfType<Position.Played>().ToList();

        return playedPositions.Any() 
            ? playedPositions.Max(p => p.Location.Fret.Value) 
            : null;
    }
}

