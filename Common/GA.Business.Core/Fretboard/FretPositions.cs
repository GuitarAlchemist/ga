using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

public class FretPositions : Positions<Position.Fretted>
{
    public FretPositions(IReadOnlyCollection<Position.Fretted> positions) 
        : base(positions)
    {
        var frets = positions.Select(position => position.Fret).Distinct().ToImmutableList();

        if (frets.Count != 1) throw new InvalidOperationException();
        Fret = frets.First();
    }

    public Fret Fret { get; }
}