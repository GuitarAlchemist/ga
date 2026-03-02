namespace GA.Domain.Core.Instruments.Primitives;

using Positions;

public readonly record struct FretRange(Fret MinFret, Fret MaxFret)
{
    public bool Contains(PositionLocation location) => location.Fret >= MinFret && location.Fret <= MaxFret;
}
