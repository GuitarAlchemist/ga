namespace GA.Business.Core.Fretboard.Positions;

using Primitives;

public readonly record struct PositionLocation(Str Str, Fret Fret)
{
    public override string ToString() => $"str: {Str}; fret: {Fret}";
}