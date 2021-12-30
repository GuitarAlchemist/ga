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
    /// <param name="Str">The <see cref="Str" />.</param>
    public sealed partial record Muted(Str Str) : Position(Str)
    {
        public override string ToString() => "x";
    }

    /// <inheritdoc cref="Position"/>
    /// <summary>
    /// An open position.
    /// </summary>
    /// <param name="Str">The <see cref="Str"/>.</param>
    public sealed partial record Open(Str Str, MidiNote MidiNote) : Fretted(Str, Fret.Open, MidiNote)
    {
        public override string ToString() => "O";
    }

    /// <inheritdoc cref="Position"/>
    /// <summary>
    /// A fretted position.
    /// </summary>
    /// <param name="Str">The <see cref="Str"/>.</param>
    /// <param name="Fret">The <see cref="Str"/>.</param>
    public partial record Fretted(Str Str, Fret Fret, MidiNote MidiNote) : Position(Str)
    {
        public override string ToString() => Fret.ToString();
    }
}
