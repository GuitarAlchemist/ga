namespace GA.Business.Core.Fretboard.Primitives;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Position
{
    /// <inheritdoc cref="Position"/>
    /// <summary>
    /// A muted position.
    /// </summary>
    /// <param name="Str">The <see cref="P:GA.Business.Core.Fretboard.Primitives.Position.Muted.Str" />.</param>
    public sealed partial record Muted(Str Str) : Position;

    /// <inheritdoc cref="Position"/>
    /// <summary>
    /// An open position.
    /// </summary>
    /// <param name="Str">The <see cref="Str"/>.</param>
    public sealed partial record Open(Str Str) : Fretted(Str, Fret.Open);

    /// <inheritdoc cref="Position"/>
    /// <summary>
    /// A fretted position.
    /// </summary>
    /// <param name="Str">The <see cref="Str"/>.</param>
    /// <param name="Fret">The <see cref="Str"/>.</param>
    public partial record Fretted(Str Str, Fret Fret) : Position;
}
