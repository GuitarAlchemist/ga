using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

public class MutedPositions : Positions<Position.Muted>
{
    public MutedPositions(IEnumerable<Position.Muted> positions) 
        : base(positions)
    {
    }
}