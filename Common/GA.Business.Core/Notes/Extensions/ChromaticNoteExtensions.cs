namespace GA.Business.Core.Notes.Extensions;

using Atonal;
using Intervals;

public static class ChromaticNoteExtensions
{
    public static Interval.Chromatic GetInterval(
        this Note.Chromatic note,
        Note.Chromatic other)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(other);
        
        if (note == other) return Interval.Chromatic.Unison;

        OrderedPitchClasses(
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

    private static void OrderedPitchClasses(
        Note note1,
        Note note2,
        out PitchClass lowPitchClass,
        out PitchClass highPitchClass,
        out bool notesInverted)
    {
        ArgumentNullException.ThrowIfNull(note1);
        ArgumentNullException.ThrowIfNull(note2);

        var pitchClass1 = note1.PitchClass;
        var pitchClass2 = note2.PitchClass;

        if (pitchClass1 > pitchClass2)
        {
            lowPitchClass = pitchClass2;
            highPitchClass = pitchClass1;
            notesInverted = true;
        }
        else
        {
            lowPitchClass = pitchClass1;
            highPitchClass = pitchClass2;
            notesInverted = false;
        }
    }
}



