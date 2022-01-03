using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

public class FrettedPositions : Positions<Position.Fretted>
{
    private readonly ILookup<Fret, Position.Fretted> _positionsByFret;

    public FrettedPositions(IReadOnlyCollection<Position.Fretted> positions) 
        : base(positions)
    {
        _positionsByFret = positions.ToLookup(position => position.Fret);
    }

    public FretPositions this[Fret fret] => new(_positionsByFret[fret].ToImmutableList());
}