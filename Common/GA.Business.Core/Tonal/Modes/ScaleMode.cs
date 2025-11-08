namespace GA.Business.Core.Tonal.Modes;

using Diatonic;
using GA.Core.Extensions;
using Intervals;
using Intervals.Primitives;
using Notes;
using Primitives.Diatonic;
using Scales;

/// <summary>
///     See https://en.wikipedia.org/wiki/Mode_(Objects)
/// </summary>
/// <remarks>
///     This class handles only the main modes (Modes could be generalized to all modal scale groups)
/// </remarks>
public abstract class ScaleMode
{
    private readonly Lazy<PrintableReadOnlyCollection<Note>> _lazyCharacteristicNotes;
    private readonly Lazy<ModeFormula> _lazyModeFormula;

    protected ScaleMode(Scale parentScale)
    {
        ParentScale = parentScale ?? throw new ArgumentNullException(nameof(parentScale));
        _lazyModeFormula = new(() => new(this));
        _lazyCharacteristicNotes = new(() => ModeCharacteristicNotes(Formula.CharacteristicIntervals).AsPrintable());
    }

    public Scale ParentScale { get; protected init; }
    public abstract string Name { get; }
    public abstract IReadOnlyCollection<Note> Notes { get; }
    public abstract IReadOnlyCollection<Interval.Simple> SimpleIntervals { get; }
    public bool IsMinorMode => SimpleIntervals.Contains(Interval.Simple.MinorThird);
    public ModeFormula Formula => _lazyModeFormula.Value;
    public PrintableReadOnlyCollection<Note> CharacteristicNotes => _lazyCharacteristicNotes.Value;

    public ScaleMode RefMode => IsMinorMode
        ? MajorScaleMode.Get(MajorScaleDegree.Aeolian)
        : MajorScaleMode.Get(MajorScaleDegree.Ionian);

    private ImmutableList<Note> ModeCharacteristicNotes(IEnumerable<ScaleModeSimpleInterval> characteristicIntervals)
    {
        var rootNote = Notes.First();
        var tuples = new List<(Note Note, Semitones Semitones)>();
        foreach (var note in Notes)
        {
            var semitones = rootNote.GetInterval(note).Semitones;
            tuples.Add((note, semitones));
        }

        var noteBySemitones = tuples.ToImmutableDictionary(tuple => tuple.Semitones, tuple => tuple.Note);

        return [..characteristicIntervals.Select(colorTone => noteBySemitones[colorTone.ToSemitones()])];
    }
}

public abstract class ScaleMode<TScaleDegree>(Scale parentScale, TScaleDegree parentScaleDegree)
    : ScaleMode(parentScale)
    where TScaleDegree : IValueObject
{
    /// <summary>
    ///     Gets the degree of this mode in the parent scale
    /// </summary>
    public TScaleDegree ParentScaleDegree { get; } = parentScaleDegree;

    /// <inheritdoc />
    public override IReadOnlyCollection<Note> Notes => new ModeNotesByScaleDegree(ParentScale)[ParentScaleDegree];

    /// <inheritdoc />
    public override IReadOnlyCollection<Interval.Simple> SimpleIntervals =>
        new ModeSimpleIntervalsByScaleDegree(ParentScale)[ParentScaleDegree];

    public IReadOnlyCollection<Interval> SimpleAndCompoundIntervals =>
        new ModeSimpleAndCompoundIntervalsByScaleDegree(ParentScale)[ParentScaleDegree];

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{Name} - {Formula}";
    }

    #region Inner classes

    private class ModeNotesByScaleDegree(Scale parentScale) : IIndexer<TScaleDegree, IReadOnlyCollection<Note>>
    {
        private readonly NotesByRotation _notesByRotation = new(parentScale);

        public IReadOnlyCollection<Note> this[TScaleDegree degree] => _notesByRotation[degree.Value - 1];
    }

    private class ModeSimpleIntervalsByScaleDegree(Scale parentScale)
        : IIndexer<TScaleDegree, IReadOnlyCollection<Interval.Simple>>
    {
        private readonly SimpleIntervalsByRotation _simpleIntervalsByRotation = new(parentScale);

        public IReadOnlyCollection<Interval.Simple> this[TScaleDegree degree] =>
            _simpleIntervalsByRotation[degree.Value - 1];
    }

    private class ModeSimpleAndCompoundIntervalsByScaleDegree(Scale parentScale)
        : IIndexer<TScaleDegree, IReadOnlyCollection<Interval>>
    {
        private readonly SimpleAndCompoundIntervalsByRotation _simpleAndCompoundIntervalsByRotation = new(parentScale);

        public IReadOnlyCollection<Interval> this[TScaleDegree degree] =>
            _simpleAndCompoundIntervalsByRotation[degree.Value - 1];
    }

    private class NotesByRotation(IReadOnlyCollection<Note> parentScaleNotes)
        : LazyIndexerBase<int, IReadOnlyCollection<Note>>(GetKeyValuePairs(parentScaleNotes))
    {
        private static IEnumerable<KeyValuePair<int, IReadOnlyCollection<Note>>> GetKeyValuePairs(
            IReadOnlyCollection<Note> parentScaleNotes)
        {
            for (var rotateCount = 0; rotateCount < parentScaleNotes.Count; rotateCount++)
            {
                var rotatedNotes = parentScaleNotes.Rotate(rotateCount).AsPrintable();
                var item = new KeyValuePair<int, IReadOnlyCollection<Note>>(rotateCount, rotatedNotes);
                yield return item;
            }
        }
    }

    private class SimpleIntervalsByRotation(IReadOnlyCollection<Note> seedScaleNotes)
        : LazyIndexerBase<int, IReadOnlyCollection<Interval.Simple>>(GetKeyValuePairs(seedScaleNotes))
    {
        private static IEnumerable<KeyValuePair<int, IReadOnlyCollection<Interval.Simple>>> GetKeyValuePairs(
            IReadOnlyCollection<Note> seedScaleNotes)
        {
            for (var rotateCount = 0; rotateCount < seedScaleNotes.Count; rotateCount++)
            {
                var intervals = GetRotatedSimpleIntervals(seedScaleNotes, rotateCount).AsPrintable();
                var item = new KeyValuePair<int, IReadOnlyCollection<Interval.Simple>>(rotateCount, intervals);
                yield return item;
            }

            yield break;

            static IReadOnlyCollection<Interval.Simple> GetRotatedSimpleIntervals(
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
                        .AsPrintable(Interval.Diatonic.Format.ShortName);

                return result;
            }
        }
    }

    private class SimpleAndCompoundIntervalsByRotation(IReadOnlyCollection<Note> seedScaleNotes)
        : LazyIndexerBase<int, IReadOnlyCollection<Interval>>(GetKeyValuePairs(seedScaleNotes))
    {
        private static IEnumerable<KeyValuePair<int, IReadOnlyCollection<Interval>>> GetKeyValuePairs(
            IReadOnlyCollection<Note> seedScaleNotes)
        {
            for (var rotateCount = 0; rotateCount < seedScaleNotes.Count; rotateCount++)
            {
                var intervals = GetRotatedSimpleIntervals(seedScaleNotes, rotateCount).AsPrintable();
                var item = new KeyValuePair<int, IReadOnlyCollection<Interval>>(rotateCount, intervals);
                yield return item;
            }

            yield break;

            static IReadOnlyCollection<Interval> GetRotatedSimpleIntervals(
                IEnumerable<Note> seedScaleNotes,
                int rotateCount)
            {
                var seedNotes = seedScaleNotes.ToImmutableList().AsPrintable();
                var rotatedNotes = seedNotes.Rotate(rotateCount);
                var startNote = rotatedNotes[0];

                var intervals = new SortedSet<Interval>();
                foreach (var endNote in rotatedNotes)
                {
                    var interval = startNote.GetInterval(endNote);
                    var isEvenIntervalSize =
                        interval.Size.Value % 2 == 0; // Even interval sizes (2, 4, 6) should yield a compound interval
                    if (isEvenIntervalSize)
                    {
                        // Add as compound interval
                        var compoundInterval = new Interval.Compound
                        {
                            Quality = interval.Quality,
                            Size = interval.Size.ToCompound()
                        };
                        intervals.Add(compoundInterval);
                    }
                    else
                    {
                        // Add as simple interval
                        intervals.Add(interval);
                    }
                }

                var result = intervals.AsPrintable(Interval.Diatonic.Format.ShortName);

                return result;
            }
        }
    }

    #endregion
}
