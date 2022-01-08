using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

public class FretPositions : Positions<Position.Fretted>
{
    public FretPositions(
        IReadOnlyCollection<Position.Fretted> positions,
        Fret fret) 
            : base(GetPositions(positions, fret))
    {
        Fret = fret;
    }

    private static IEnumerable<Position.Fretted> GetPositions(
        IReadOnlyCollection<Position.Fretted> positions,
        Fret fret)
    {
        var result = positions
            .Where(fretted => fretted.Fret == fret)
            .ToImmutableList();

        return result;
    }

    public Fret Fret { get; }
}