using GA.Business.Core.Notes;

namespace GA.Business.Core.Fretboard.Primitives;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Position(Str Str)
{
    /// <inheritdoc cref="Position"/>
    /// <summary>
    /// A muted position.
    /// </summary>
    public sealed partial record Muted(Str Str) : Position(Str);

    /// <inheritdoc cref="Position"/>
    /// <summary>
    /// An open position.
    /// </summary>
    public sealed partial record Open(Str Str, Pitch Pitch) : Fretted(Str, Fret.Open, Pitch);

    /// <inheritdoc cref="Position"/>
    /// <summary>
    /// A fretted position.
    /// </summary>
    public partial record Fretted(Str Str, Fret Fret, Pitch Pitch) : Position(Str)
    {
    }
}
