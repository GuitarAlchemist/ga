namespace GA.Domain.Core.Primitives.Extensions;

using System.Collections.Generic;

public static class ChromaticNoteExtensions
{
    public static ChromaticNoteSet ToChromaticNoteSet(this IEnumerable<Note.Chromatic> notes)
    {
        return new(notes);
    }
}
