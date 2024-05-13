namespace GA.Business.Core.Notes;

using Atonal;

[PublicAPI]
public sealed class ChromaticNoteSet(ImmutableSortedSet<Note.Chromatic> notes) : PrintableImmutableSet<Note.Chromatic>(notes)
{
    /// <summary>
    /// Empty <see cref="ChromaticNoteSet"/>
    /// </summary>
    public static readonly ChromaticNoteSet Empty = [];
    
    public ChromaticNoteSet(params Note.Chromatic[] notes) : this(notes.ToImmutableSortedSet()) { }
    public ChromaticNoteSet(IEnumerable<Note.Chromatic> notes) : this(GetSet(notes)) { }

    public IEnumerable<PitchClass> PitchClassCollection => Items.Select(note => note.PitchClass);
}
