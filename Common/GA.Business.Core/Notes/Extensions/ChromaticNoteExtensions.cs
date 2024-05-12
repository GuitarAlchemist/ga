namespace GA.Business.Core.Notes.Extensions;

public static class ChromaticNoteExtensions
{
    public static ChromaticNoteSet ToChromaticNoteSet(this IEnumerable<Note.Chromatic> notes) => new(notes);
}