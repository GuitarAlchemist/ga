namespace GA.Business.Core.Fretboard.Positions;

using Primitives;

public readonly record struct RelativePositionLocation(Str Str, RelativeFret RelativeFret) : IStr
{
}
