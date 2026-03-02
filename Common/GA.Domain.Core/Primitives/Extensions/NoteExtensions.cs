namespace GA.Domain.Core.Primitives.Extensions;

using Intervals;
using Notes;
using Theory.Atonal;
using Interval = Intervals.Interval;

/// <summary>
///     Extension methods for Note types
/// </summary>
public static class NoteExtensions
{
    private static IntervalQuality DetermineQuality(SimpleIntervalSize size, Semitones semitones)
    {
        // Grounded in interval quality classification (perfect/major/minor/augmented/diminished).
        // https://en.wikipedia.org/wiki/Interval_(music)#Quality
        // Expected semitones for perfect/imperfect intervals
        var expectedSemitones = size.Value switch
        {
            1 => 0, // Unison
            2 => 2, // Major 2nd
            3 => 4, // Major 3rd
            4 => 5, // Perfect 4th
            5 => 7, // Perfect 5th
            6 => 9, // Major 6th
            7 => 11, // Major 7th
            _ => 0
        };

        // Handle perfect intervals
        var consonance = size.Consonance;
        var difference = semitones.Value - expectedSemitones;
        if (consonance == IntervalConsonance.Perfect)
        {
            return difference switch
            {
                -2 => IntervalQuality.DoublyDiminished,
                -1 => IntervalQuality.Diminished,
                0 => IntervalQuality.Perfect,
                1 => IntervalQuality.Augmented,
                2 => IntervalQuality.DoublyAugmented,
                _ => IntervalQuality.Perfect
            };
        }

        // Handle imperfect intervals
        return difference switch
        {
            -3 => IntervalQuality.DoublyDiminished,
            -2 => IntervalQuality.Diminished,
            -1 => IntervalQuality.Minor,
            0 => IntervalQuality.Major,
            1 => IntervalQuality.Augmented,
            2 => IntervalQuality.DoublyAugmented,
            _ => IntervalQuality.Major
        };
    }

    /// <summary>
    ///     Extensions for 2 notes
    /// </summary>
    /// <param name="note1">The first <see cref="Note" /></param>
    extension(Note note1)
    {
        /// <summary>
        ///     Gets the interval class between two notes
        /// </summary>
        /// <param name="note2">The second <see cref="Note" /></param>
        /// <remarks>
        ///     Example: <c>Note.Chromatic.C.GetIntervalClass(Note.Chromatic.G)</c> returns <c>IntervalClass.PerfectFifth</c>.
        /// </remarks>
        public IntervalClass GetIntervalClass(Note note2)
        {
            var pitchClass1 = note1.PitchClass;
            var pitchClass2 = note2.PitchClass;
            var semitones = (pitchClass2.Value - pitchClass1.Value + 12) % 12;
            return IntervalClass.FromValue(semitones);
        }

        /// <summary>
        ///     Gets the simple interval between two notes
        /// </summary>
        /// <param name="note2">The second <see cref="Note" /></param>
        /// <remarks>
        ///     Example: <c>Note.Sharp.C.GetInterval(Note.Sharp.G)</c> returns <c>Interval.Simple.P5</c>.
        /// </remarks>
        public Interval.Simple GetInterval(Note note2)
        {
            var accidented1 = note1.ToAccidented();
            var accidented2 = note2.ToAccidented();
            return accidented1.GetInterval(accidented2);
        }
    }

    extension(Note.Accidented note1)
    {
        /// <summary>
        ///     Gets the simple interval between two accidented notes
        /// </summary>
        /// <remarks>
        ///     Example: <c>new Note.Accidented(NaturalNote.F).GetInterval(new Note.Accidented(NaturalNote.B))</c> returns
        ///     <c>Interval.Simple.A4</c>.
        /// </remarks>
        public Interval.Simple GetInterval(Note.Accidented note2)
        {
            // Calculate the interval size based on natural notes
            var size = note2.NaturalNote - note1.NaturalNote;

            // Calculate the semitones
            var semitones = (note2.PitchClass.Value - note1.PitchClass.Value + 12) % 12;

            // Determine the quality based on the size and semitones
            var quality = DetermineQuality(size, new() { Value = semitones });

            return new() { Size = size, Quality = quality };
        }
    }
}
