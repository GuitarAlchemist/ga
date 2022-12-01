namespace GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes.Primitives;

using Positions;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Position
{
    /// <inheritdoc cref="Position"/>
    public sealed partial record Muted(Str Str) : Position;

    /// <inheritdoc cref="Position"/>
    public sealed partial record Played(PositionLocation Location, MidiNote MidiNote) : Position;
}
