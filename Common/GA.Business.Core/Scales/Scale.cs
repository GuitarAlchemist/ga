using System.Collections;
using System.Collections.Immutable;
using GA.Business.Core.Notes;
using GA.Business.Core.Tonal;
using GA.Core;
using static GA.Business.Core.Notes.Note.AccidentedNote;

namespace GA.Business.Core.Scales;

public class Scale : IReadOnlyCollection<Note>
{
    public static Scale Major => new(Key.Major.C.GetNotes());
    public static Scale NaturalMinor => new(Key.Major.A.GetNotes());
    public static Scale HarmonicMinor => new(A, B, C, D, E, F, GSharp, A);
    public static Scale MelodicMinor => new(A, B, C, D, E, FSharp, GSharp, A);
    public static Scale MajorPentatonic => new(C, D, E, G, A);
    public static Scale WholeTone => new(C, D, E, FSharp, GSharp, ASharp);

    private readonly IReadOnlyCollection<Note> _notes;

    public Scale(params Note.AccidentedNote[] notes) 
        : this(notes.AsEnumerable())
    {
    }

    public Scale(IEnumerable<Note> notes)
    {
        if (notes == null) throw new ArgumentNullException(nameof(notes));
        _notes = notes.ToImmutableList().AsPrintable();
    }

    public IEnumerator<Note> GetEnumerator() => _notes.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _notes).GetEnumerator();
    public int Count => _notes.Count;
}

