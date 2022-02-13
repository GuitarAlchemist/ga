namespace GA.Business.Core.Tonal.Modes;

using System.Collections.Immutable;

using GA.Core;
using Intervals;
using Notes;
using Scales;

/// <summary>
/// See https://en.wikipedia.org/wiki/Mode_(music)
/// </summary>
public abstract class ScaleMode
{
    protected ScaleMode(Scale parentScale)
    {
        ParentScale = parentScale ?? throw new ArgumentNullException(nameof(parentScale));
    } 

    public Scale ParentScale { get; }
    public abstract string Name { get; }
    public abstract IReadOnlyCollection<Note> Notes { get; }
    public abstract IReadOnlyCollection<Interval.Simple> Intervals { get; }
    public bool IsMinorMode => Intervals.Contains(Interval.Simple.MinorThird);
    public ModeFormula Formula => new(this);
    public ScaleMode RefMode => IsMinorMode ? MajorScaleMode.Aeolian : MajorScaleMode.Ionian;
    public int Identity => Intervals.ToPitchClassSet().GetIdentity();
}

public abstract class ScaleMode<TScaleDegree> : ScaleMode
    where TScaleDegree : IReadOnlyValue
{
    public TScaleDegree ParentScaleDegree { get; }
    public override IReadOnlyCollection<Note> Notes => new ModeNotesByScaleDegree(ParentScale)[ParentScaleDegree];
    public override IReadOnlyCollection<Interval.Simple> Intervals => new ModeIntervalsByScaleDegree(ParentScale)[ParentScaleDegree];
    public override string ToString() => $"{Name} - {Formula}";

    protected ScaleMode(
        Scale parentScale, 
        TScaleDegree parentScaleDegree) 
            : base(parentScale)
    {
        ParentScaleDegree = parentScaleDegree;
    }

    #region Inner classes

    private class ModeNotesByScaleDegree : IIndexer<TScaleDegree, IReadOnlyCollection<Note>>
    {
        public ModeNotesByScaleDegree(Scale parentScale) => _notesByRotation = new(parentScale);
        private readonly NotesByRotation _notesByRotation;

        public IReadOnlyCollection<Note> this[TScaleDegree degree] => _notesByRotation[degree.Value - 1];
    }

    private class ModeIntervalsByScaleDegree : IIndexer<TScaleDegree, IReadOnlyCollection<Interval.Simple>>
    {
        public ModeIntervalsByScaleDegree(Scale parentScale) => _intervalsByRotation = new(parentScale);
        private readonly IntervalsByRotation _intervalsByRotation;

        public IReadOnlyCollection<Interval.Simple> this[TScaleDegree degree] => _intervalsByRotation[degree.Value - 1];
    }

    private class NotesByRotation : LazyIndexerBase<int, IReadOnlyCollection<Note>>
    {
        public NotesByRotation(IReadOnlyCollection<Note> parentScaleNotes) 
            : base(GetKeyValuePairs(parentScaleNotes))
        {
        }

        private static IEnumerable<KeyValuePair<int, IReadOnlyCollection<Note>>> GetKeyValuePairs(IReadOnlyCollection<Note> parentScaleNotes)
        {
            for (var rotateCount = 0; rotateCount < parentScaleNotes.Count; rotateCount++)
            {
                var rotatedNotes = parentScaleNotes.Rotate(rotateCount).AsPrintable();
                var item = new KeyValuePair<int, IReadOnlyCollection<Note>>(rotateCount, rotatedNotes);
                yield return item;
            }
        }
    }

    private class IntervalsByRotation : LazyIndexerBase<int, IReadOnlyCollection<Interval.Simple>>
    {
        // parentScaleNotes = Key.MajorScaleMode.C.GetNotes();
        public IntervalsByRotation(IReadOnlyCollection<Note> seedScaleNotes) 
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

