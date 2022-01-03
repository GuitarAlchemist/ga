using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard;

public class OpenPositions : Positions<Position.Open>
{
    public OpenPositions(IEnumerable<Position.Open> positions) 
        : base(positions)
    {
    }

    public override string ToString() => string.Join(" ", this.OrderBy(position => position.Str).Select(open => open.Pitch));
}