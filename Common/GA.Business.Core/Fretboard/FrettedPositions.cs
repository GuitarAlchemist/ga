namespace GA.Business.Core.Fretboard;

using Primitives;

public class FrettedPositions : Positions<Position.Fretted>
{
    public FrettedPositions(IEnumerable<Position.Fretted> positions) 
        : base(positions)
    {
    }

    public FretPositions this[Fret fret] => new(this, fret);
}