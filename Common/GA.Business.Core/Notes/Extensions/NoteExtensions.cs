namespace GA.Business.Core.Notes.Extensions;

using Atonal.Primitives;
using Intervals;
using Intervals.Primitives;

/// <summary>
/// Extension methods for Note types
/// </summary>
public static class NoteExtensions
{
    /// <summary>
    /// Gets the interval class between two notes
    /// </summary>
    public static IntervalClass GetIntervalClass(this Note note1, Note note2)
    {
        var pitchClass1 = note1.PitchClass;
        var pitchClass2 = note2.PitchClass;
        var semitones = (pitchClass2.Value - pitchClass1.Value + 12) % 12;
        return IntervalClass.FromValue(semitones);
    }

    /// <summary>
    /// Gets the simple interval between two notes
    /// </summary>
    public static Interval.Simple GetInterval(this Note note1, Note note2)
    {
        var accidented1 = note1.ToAccidented();
        var accidented2 = note2.ToAccidented();
        return GetInterval(accidented1, accidented2);
    }

    /// <summary>
    /// Gets the simple interval between two accidented notes
    /// </summary>
    public static Interval.Simple GetInterval(this Note.Accidented note1, Note.Accidented note2)
    {
        // Calculate the interval size based on natural notes
        var size = note2.NaturalNote - note1.NaturalNote;

        // Calculate the semitones
        var semitones = (note2.PitchClass.Value - note1.PitchClass.Value + 12) % 12;

        // Determine the quality based on the size and semitones
        var quality = DetermineQuality(size, new()
            { Value = semitones });

        return new()
            { Size = size, Quality = quality };
    }

    private static IntervalQuality DetermineQuality(SimpleIntervalSize size, Semitones semitones)
    {
        // Get the consonance type for this interval size
        var consonance = size.Consonance;

        // Expected semitones for perfect/major intervals
        var expectedSemitones = size.Value switch
        {
            1 => 0,  // Unison
            2 => 2,  // Major 2nd
            3 => 4,  // Major 3rd
            4 => 5,  // Perfect 4th
            5 => 7,  // Perfect 5th
            6 => 9,  // Major 6th
            7 => 11, // Major 7th
            _ => 0
        };

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
        else
        {
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
    }
}

