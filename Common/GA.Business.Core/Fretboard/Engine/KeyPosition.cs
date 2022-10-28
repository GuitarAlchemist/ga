namespace GA.Business.Core.Fretboard.Engine;

using Primitives;
using Notes;

public record KeyPosition(Position.Played Position, Note.KeyNote KeyNote)
{
    public override string ToString() => $"{Position.Location} - {KeyNote}";
}