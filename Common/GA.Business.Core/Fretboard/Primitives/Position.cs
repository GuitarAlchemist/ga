using GA.Business.Core.Notes;

namespace GA.Business.Core.Fretboard.Primitives;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Position(Str Str)
{
    /// <inheritdoc cref="Position"/>
    public sealed partial record Muted(Str Str) : Position(Str);

    /// <inheritdoc cref="Position"/>
    public partial record Fretted(Str Str, Fret Fret, Pitch Pitch) : Position(Str);

    /// <inheritdoc cref="Position"/>
    public sealed partial record Open(Str Str, Pitch Pitch) : Fretted(Str, Fret.Open, Pitch);
}
