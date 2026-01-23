namespace GA.Domain.Core.Primitives.Extensions;

using System.Collections.Generic;
using System.Linq;

public static class NaturalNoteExtensions
{
    public static IEnumerable<Note.Sharp> ToSharpNotes(this IEnumerable<NaturalNote> items)
    {
        return items.Select(note => note.ToSharpNote());
    }

    public static IEnumerable<Note.Sharp> ToSharpNotes(this IEnumerable<NaturalNote> items, SharpAccidental accidental)
    {
        return items.Select(note => note.ToSharpNote(accidental));
    }

    public static IEnumerable<Note.Flat> ToFlatNotes(this IEnumerable<NaturalNote> items)
    {
        return items.Select(note => note.ToFlatNote());
    }

    public static IEnumerable<Note.Flat> ToFlatNotes(this IEnumerable<NaturalNote> items, FlatAccidental accidental)
    {
        return items.Select(note => note.ToFlatNote(accidental));
    }
}
