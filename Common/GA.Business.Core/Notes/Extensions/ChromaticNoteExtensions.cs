using GA.Business.Core.Intervals;
using GA.Business.Core.Notes.Primitives;

namespace GA.Business.Core.Notes.Extensions
{
    public static class ChromaticNoteExtensions
    {
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
}
