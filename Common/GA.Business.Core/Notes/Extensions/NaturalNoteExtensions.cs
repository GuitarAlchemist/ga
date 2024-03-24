using GA.Business.Core.Notes.Primitives;

namespace GA.Business.Core.Notes.Extensions
{
    public static class NaturalNoteExtensions
    {
        public static IEnumerable<Note.Sharp> ToSharpNotes(this IEnumerable<NaturalNote> items) => items.Select(note => note.ToSharpNote());
        public static IEnumerable<Note.Sharp> ToSharpNotes(this IEnumerable<NaturalNote> items, SharpAccidental accidental) => items.Select(note => note.ToSharpNote(accidental));
        
        public static IEnumerable<Note.Flat> ToFlatNotes(this IEnumerable<NaturalNote> items) => items.Select(note => note.ToFlatNote());
        public static IEnumerable<Note.Flat> ToFlatNotes(this IEnumerable<NaturalNote> items, FlatAccidental accidental) => items.Select(note => note.ToFlatNote(accidental));
    }
}
