namespace GA.Business.Core.Tonal.Modes;

using System.Collections.Immutable;

using Intervals;
using Notes;
using Scales;
using GA.Core;

/// <summary>
/// See https://en.wikipedia.org/wiki/Mode_(music)
/// </summary>
public abstract class ScaleMode
{
    protected ScaleMode(Scale scale) => Scale = scale ?? throw new ArgumentNullException(nameof(scale));

    public Scale Scale { get; }
    public abstract string Name { get; }
    public abstract IReadOnlyCollection<Interval.Simple> Intervals { get; }
    public bool IsMinorMode => Intervals.Contains(Interval.Simple.MinorThird);
    public ModeFormula Formula => new(this);
    public ScaleMode RefMode => IsMinorMode ? MajorScaleMode.Aeolian : MajorScaleMode.Ionian;
}

public abstract class ScaleMode<TScaleDegree> : ScaleMode
    where TScaleDegree : IReadOnlyValue
{
    public TScaleDegree ScaleDegree { get; }
    public override IReadOnlyCollection<Interval.Simple> Intervals => new ModeIntervalsByScaleDegree(Scale)[ScaleDegree];
    public override string ToString() => $"{Name} - {Formula}";

    protected ScaleMode(Scale scale, TScaleDegree scaleDegree) : base(scale)
    {
        ScaleDegree = scaleDegree;
    }

    #region Inner classes

    private class ModeIntervalsByScaleDegree : IIndexer<TScaleDegree, IReadOnlyCollection<Interval.Simple>>
    {
        public ModeIntervalsByScaleDegree(Scale scale) => _intervalsByNotesRotation = new(scale);
        private readonly IntervalsByNotesRotation _intervalsByNotesRotation;

        public IReadOnlyCollection<Interval.Simple> this[TScaleDegree degree] => _intervalsByNotesRotation[degree.Value - 1];
    }

    private class IntervalsByNotesRotation : LazyIndexerBase<int, IReadOnlyCollection<Interval.Simple>>
    {
        // seedScaleNotes = Key.MajorScaleMode.C.GetNotes();
        public IntervalsByNotesRotation(IReadOnlyCollection<Note> seedScaleNotes) 
            : base(GetKeyValuePairs(seedScaleNotes))
        {
        }

        private static IEnumerable<KeyValuePair<int, IReadOnlyCollection<Interval.Simple>>> GetKeyValuePairs(IReadOnlyCollection<Note> seedScaleNotes)
        {
            for (var rotateCount = 0; rotateCount < seedScaleNotes.Count; rotateCount++)
            {
                var intervals = GetRotatedIntervals(seedScaleNotes, rotateCount).AsPrintable();
                var item = new KeyValuePair<int, IReadOnlyCollection<Interval.Simple>>(rotateCount, intervals);
                yield return item;
            }

            static IReadOnlyCollection<Interval.Simple> GetRotatedIntervals(
                IEnumerable<Note> seedScaleNotes,
                int rotateCount)
            {
                var seedNotes = seedScaleNotes.ToImmutableList().AsPrintable();
                var rotatedNotes = seedNotes.Rotate(rotateCount);
                var startNote = rotatedNotes[0];
                var result = 
                    rotatedNotes
                        .Select(endNote => startNote.GetInterval(endNote))
                        .ToImmutableList()
                        .AsPrintable(Interval.Simple.Format.ShortName);

                return result;
            }
        }
    }

    #endregion
}

