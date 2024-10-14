namespace GA.Business.Core.Fretboard.Engine;

using Primitives;
using Notes;

/// <summary>
/// Represents a fretboard position, associated with a key note
/// </summary>
/// <param name="Position">The <see cref="Position.Played"/></param>
/// <param name="KeyNote">The <see cref="Note.KeyNote"/></param>
public sealed record KeyPosition(
    Position.Played Position, 
    Note.KeyNote KeyNote)
{
    /// <inheritdoc />
    public override string ToString() => $"{Position.Location} - {KeyNote}";
}