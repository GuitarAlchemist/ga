namespace GA.Business.Core.Fretboard.Engine;

using Primitives;
using Notes;

public record KeyPosition(Position.Fretted Position, Note.KeyNote KeyNote)
{
    public override string ToString() => $"str {Position.Str}, fret {Position.Fret} - {KeyNote}";
}