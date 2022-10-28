namespace GA.Business.Core.Fretboard.Primitives;

using Positions;
using Notes;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Position
{
    /// <inheritdoc cref="Position"/>
    public sealed partial record Muted(Str Str) : Position;

    /// <inheritdoc cref="Position"/>
    public sealed partial record Played(PositionLocation Location, Pitch Pitch) : Position;
}
