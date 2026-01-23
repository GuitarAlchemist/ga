namespace GA.Domain.Core.Instruments.Positions;

using Primitives;

public readonly record struct RelativePositionLocation(Str Str, RelativeFret RelativeFret) : IStr
{
}
