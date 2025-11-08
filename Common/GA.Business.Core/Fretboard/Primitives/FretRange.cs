namespace GA.Business.Core.Fretboard.Primitives;

using Positions;

public readonly record struct FretRange(Fret MinFret, Fret MaxFret)
{
    public bool Contains(PositionLocation location)
    {
        return location.Fret >= MinFret && location.Fret <= MaxFret;
    }
}
