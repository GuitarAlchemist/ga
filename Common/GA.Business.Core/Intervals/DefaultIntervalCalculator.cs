namespace GA.Business.Core.Intervals;

using Notes;
using Notes.Primitives;
using Tonal;
using Primitives;

/// <summary>
/// Default implementation of <see cref="IIntervalCalculator"/>
/// </summary>
public class DefaultIntervalCalculator : IIntervalCalculator
{
    private static readonly Lazy<Dictionary<NaturalNote, Key.Major>> _lazyMajorKeyByNaturalNote = new(() => new()
    {
        [NaturalNote.C] = Key.Major.C,
        [NaturalNote.D] = Key.Major.D,
        [NaturalNote.E] = Key.Major.E,
        [NaturalNote.F] = Key.Major.F,
        [NaturalNote.G] = Key.Major.G,
        [NaturalNote.A] = Key.Major.A,
        [NaturalNote.B] = Key.Major.B
    });

    public Result<Interval, string> GetInterval(Note note1, Note note2)
    {
        var accidentedNote1 = note1.ToAccidented();
        var accidentedNote2 = note2.ToAccidented();

        return GetInterval(accidentedNote1, accidentedNote2);
    }

    /// <summary>
    ///     Gets the interval between two accidented notes
    /// </summary>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    private static Interval.Simple GetInterval(
        Note.Accidented start,
        Note.Accidented end)
    {
        if (start == end)
        {
            return Interval.Simple.Unison;
        }

        var majorKeyByNaturalNote = _lazyMajorKeyByNaturalNote.Value;
        if (!majorKeyByNaturalNote.TryGetValue(start.NaturalNote, out var key))
        {
            throw new InvalidOperationException($"No major key found for {start.NaturalNote}");
        }

        var size = end.NaturalNote - start.NaturalNote;
        var qualityIncrement = GetQualityIncrement(key, start, end);
        var quality = GetQuality(size, qualityIncrement);
        var result = new Interval.Simple
        {
            Size = size,
            Quality = quality
        };

        return result;

        static Semitones GetQualityIncrement(
            Key key,
            Note.Accidented startNote,
            Note.Accidented endNote)
        {
            var result = Semitones.None;

            // Quality - Start note
            if (startNote.Accidental.HasValue)
            {
                result -= startNote.Accidental.Value.ToSemitones();
            }

            // Quality - End note
            var (endNaturalNote, endNoteAccidental) = endNote;
            if (key.KeySignature.IsNoteAccidented(endNaturalNote))
            {
                var expectedEndNoteAccidental =
                    key.AccidentalKind == AccidentalKind.Flat
                        ? Accidental.Flat
                        : Accidental.Sharp;

                if (endNoteAccidental == expectedEndNoteAccidental)
                {
                    return result;
                }

                var actualEndNoteAccidentalValue = endNoteAccidental?.Value ?? 0;
                var endNoteAccidentalDelta = actualEndNoteAccidentalValue - expectedEndNoteAccidental.Value;
                result += endNoteAccidentalDelta;
            }
            else if (endNoteAccidental.HasValue)
            {
                result += endNoteAccidental.Value.ToSemitones();
            }

            return result;
        }

        static IntervalQuality GetQuality(
            SimpleIntervalSize number,
            Semitones qualityIncrement)
        {
            if (number.Consonance == IntervalConsonance.Perfect)
            {
                // Handle perfect intervals (unison, fourth, fifth, octave)
                if (qualityIncrement.Value <= -2)
                {
                    return IntervalQuality.DoublyDiminished;
                }

                if (qualityIncrement.Value == -1)
                {
                    return IntervalQuality.Diminished;
                }

                if (qualityIncrement.Value == 0)
                {
                    return IntervalQuality.Perfect;
                }

                if (qualityIncrement.Value == 1)
                {
                    return IntervalQuality.Augmented;
                }

                if (qualityIncrement.Value >= 2)
                {
                    return IntervalQuality.DoublyAugmented;
                }

                // Default fallback (should never reach here)
                return IntervalQuality.Perfect;
            }

            // Handle imperfect intervals (seconds, thirds, sixths, sevenths)
            if (qualityIncrement.Value <= -3)
            {
                return IntervalQuality.DoublyDiminished;
            }

            if (qualityIncrement.Value == -2)
            {
                return IntervalQuality.Diminished;
            }

            if (qualityIncrement.Value == -1)
            {
                return IntervalQuality.Minor;
            }

            if (qualityIncrement.Value == 0)
            {
                return IntervalQuality.Major;
            }

            if (qualityIncrement.Value == 1)
            {
                return IntervalQuality.Augmented;
            }

            if (qualityIncrement.Value >= 2)
            {
                return IntervalQuality.DoublyAugmented;
            }

            // Default fallback (should never reach here)
            return IntervalQuality.Major;
        }
    }
}
