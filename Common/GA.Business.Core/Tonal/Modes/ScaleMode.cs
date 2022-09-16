using GA.Business.Core.SetTheory;

namespace GA.Business.Core.Tonal.Modes;

using GA.Business.Core.Intervals.Primitives;
using GA.Core;
using GA.Core.Extensions;
using Intervals;
using Notes;
using Scales;

/// <summary>
/// See https://en.wikipedia.org/wiki/Mode_(Objects)
/// </summary>
/// <remarks>
/// This class handles only the main modes (Modes could be generalized to all modal scale groups)
/// </remarks>
public abstract class ScaleMode
{
    private readonly Lazy<ModeFormula> _lazyModeFormula;
    private readonly Lazy<IReadOnlyCollection<Note>> _lazyColorNotes;

    protected ScaleMode(Scale parentScale)
    {
        ParentScale = parentScale ?? throw new ArgumentNullException(nameof(parentScale));
        _lazyModeFormula = new(() => new(this));
        _lazyColorNotes = new(() => ModeColorNotes(Formula.ColorTones).AsPrintable());
    } 

    public Scale ParentScale { get; }
    public abstract string Name { get; }
    public abstract IReadOnlyCollection<Note> Notes { get; }
    public abstract IReadOnlyCollection<Interval.Simple> Intervals { get; }
    public bool IsMinorMode => Intervals.Contains(Interval.Simple.MinorThird);
    public ModeFormula Formula => _lazyModeFormula.Value;
    public IReadOnlyCollection<Note> ColorNotes => _lazyColorNotes.Value;
    public ScaleMode RefMode => IsMinorMode ? MajorScaleMode.Aeolian : MajorScaleMode.Ionian;
    public PitchClassSetIdentity Identity => PitchClassSetIdentity.FromNotes(Notes);

    private ImmutableList<Note> ModeColorNotes(
        IEnumerable<ModeInterval> colorTones)
    {
        var rootNote = Notes.First();
        var tuples = new List<(Note Note, Semitones Semitones)>();
        foreach (var note in Notes)
        {
            var semitones = rootNote.GetInterval(note).ToSemitones();
            tuples.Add((note, semitones));
        }
        var noteBySemitones = tuples.ToImmutableDictionary(tuple => tuple.Semitones, tuple => tuple.Note);

        return colorTones.Select(colorTone => noteBySemitones[colorTone.ToSemitones()]).ToImmutableList();
    }
}

public abstract class ScaleMode<TScaleDegree> : ScaleMode
    where TScaleDegree : IValueObject
{
    public TScaleDegree ParentScaleDegree { get; }
    public override IReadOnlyCollection<Note> Notes => new ModeNotesByScaleDegree(ParentScale)[ParentScaleDegree];
    public override IReadOnlyCollection<Interval.Simple> Intervals => new ModeIntervalsByScaleDegree(ParentScale)[ParentScaleDegree];
    public override string ToString() => $"{Name} - {Formula}";

    /*
    TODO: Provide abstraction for this:

    public static IReadOnlyCollection<MajorScaleMode> Items => MajorScaleDegree.Items.Select(degree => new MajorScaleMode(degree)).ToImmutableList();
    public static MajorScaleMode Get(MajorScaleDegree degree) => _lazyModeByDegree.Value[degree];
    public static MajorScaleMode Get(int degree) => _lazyModeByDegree.Value[degree];
    private static Lazy<ScaleModeCollection<MajorScaleDegree, MajorScaleMode>> _lazyModeByDegree = new(() => new(Items));
    */
    
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

