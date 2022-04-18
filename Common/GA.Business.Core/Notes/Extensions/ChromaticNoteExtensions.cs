namespace GA.Business.Core.Notes.Extensions;

using Intervals;
using Primitives;

public static class ChromaticNoteExtensions
{
    public static PitchClassSet ToPitchClassSet(this IEnumerable<Note> notes) => 
        new(notes.Select(note => note.PitchClass));

    public static Interval.Chromatic GetInterval(
        this Note.Chromatic note,
        Note.Chromatic other)
    {
        if (note == null) throw new ArgumentNullException(nameof(note));
        if (other == null) throw new ArgumentNullException(nameof(other));
        if (note == other) return Interval.Chromatic.Unison;

        GetOrderedPitchClasses(
            note,
            other,
            out var lowPitchClass,
            out var highPitchClass,
            out var notesInverted);
        var result = highPitchClass - lowPitchClass;

        // Result
        if (notesInverted) result = !result;
        return result;
    }

    private static void GetOrderedPitchClasses(
        Note note1,
        Note note2,
        out PitchClass lowPitchClass,
        out PitchClass highPitchClass,
        out bool notesInverted)
    {
        if (note1 == null) throw new ArgumentNullException(nameof(note1));
        if (note2 == null) throw new ArgumentNullException(nameof(note2));

        var PitchClass1 = note1.PitchClass;
        var PitchClass2 = note2.PitchClass;

        if (PitchClass1 > PitchClass2)
        {
            lowPitchClass = PitchClass2;
            highPitchClass = PitchClass1;
            notesInverted = true;
        }
        else
        {
            lowPitchClass = PitchClass1;
            highPitchClass = PitchClass2;
            notesInverted = false;
        }
    }
}



