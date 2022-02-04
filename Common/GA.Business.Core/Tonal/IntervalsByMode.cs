using System.Collections.Immutable;
using GA.Business.Core.Intervals;
using GA.Business.Core.Notes;
using GA.Core;

namespace GA.Business.Core.Tonal;

public class IntervalsByMode : LazyIndexerBase<int, IReadOnlyCollection<Interval.Simple>>
{
    // seedScaleNotes = Key.Major.C.GetNotes();
    public IntervalsByMode(IReadOnlyCollection<Note> seedScaleNotes) 
        : base(GetKeyValuePairs(seedScaleNotes))
    {
    }

    private static IEnumerable<KeyValuePair<int, IReadOnlyCollection<Interval.Simple>>> GetKeyValuePairs(IReadOnlyCollection<Note> seedScaleNotes)
    {
        for (var rotate = 0; rotate < seedScaleNotes.Count; rotate++)
        {
            var intervals = GetRotatedIntervals(seedScaleNotes, rotate).AsPrintable();
            var item = new KeyValuePair<int, IReadOnlyCollection<Interval.Simple>>(rotate, intervals);
            yield return item;
        }

        static IReadOnlyCollection<Interval.Simple> GetRotatedIntervals(
            IReadOnlyCollection<Note> seedScaleNotes,
            int rotateCount)
        {
            var seedNotes = seedScaleNotes.Take(7).ToImmutableList().AsPrintable();
            var rotatedNotes = seedNotes.Rotate(rotateCount);
            var startNote = rotatedNotes[0];
            var result = 
                rotatedNotes
                     .Select(endNote => startNote.GetInterval(endNote))
                     .ToImmutableList()
                     .AsPrintable(Interval.Simple.Format.AccidentedName);

            return result;
        }
    }
}