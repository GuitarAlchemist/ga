namespace GA.Business.Core.Scales;

using Atonal;
using Atonal.Primitives;
using Extensions;
using Intervals;
using Notes;

/// <summary>
/// A scale
/// </summary>
/// <remarks>
/// See https://www.youtube.com/c/TheExcitingUniverseofmusictheory/videos
/// http://allthescales.org/
/// https://ianring.com/musictheory/
/// https://ianring.com/musictheory/scales/
/// https://chromatone.center/theory/scales/study.html
/// </remarks>
public class Scale : IStaticReadonlyCollection<Scale>,
                     IReadOnlyCollection<Note>
{
    #region IStaticReadonlyCollection<Scale> Members

    public static IReadOnlyCollection<Scale> Items =>
        PitchClassSet.Items
            .Where(set => set.Id.IsScale)
            .Select(set => FromId(set.Id))
            .ToLazyCollection();

    #endregion

    public static Scale Major => new("C D E F G A B");
    public static Scale NaturalMinor => Minor.Natural;
    public static Scale HarmonicMinor => Minor.Harmonic;
    public static Scale MelodicMinor => Minor.Melodic;
    public static Scale MajorPentatonic => new("C D E G A");

    /// <summary>
    /// https://ianring.com/musictheory/scales/1365
    /// </summary>
    public static Scale WholeTone => new("C D E F# G# A#");
    public static Scale ChromaticSharp => new("C C# D D# E F F# G G# A A# B");
    public static Scale ChromaticFlat => new("C Db D Eb E F Gb G Ab A Bb B");
    public static Scale FromId(PitchClassSetId id) => new(id.Notes);

    private readonly PrintableReadOnlyCollection<Note> _notes;

    public Scale(params Note.Accidented[] notes) 
        : this(notes.AsEnumerable())
    {
    }

    public Scale(IEnumerable<Note> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        
        _notes = notes.ToImmutableList().AsPrintable();
        PitchClassSet = _notes.ToPitchClassSet();
        IntervalClassVector = PitchClassSet.IntervalClassVector;
        Intervals = new LazyScaleIntervals(_notes);
    }

    public Scale(string notes) 
        : this(AccidentedNoteCollection.Parse(notes))
    {
    }

    public IReadOnlyCollection<Interval.Simple> Intervals { get; }
    public PitchClassSet PitchClassSet { get; }
    public IntervalClassVector IntervalClassVector { get; }
    public bool IsModal => PitchClassSet.IsModal;
    public ModalFamily? ModalFamily => ModalFamily.TryGetValue(IntervalClassVector, out var modalFamily) ? modalFamily : null;

    public IEnumerator<Note> GetEnumerator() => _notes.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _notes).GetEnumerator();
    public int Count => _notes.Count;

    public override string ToString()
    {
        var scaleName = string.Empty; // TODO
        if (string.IsNullOrEmpty(scaleName)) return _notes.ToString()!;
        return $"{scaleName} - {_notes}";
    }

    #region Inner classes

    public static class Minor
    {
        public static Scale Natural => new("A B C D E F G");
        public static Scale Harmonic => new("A B C D E F G#");
        public static Scale Melodic => new("A B C D E F# G#");
    }

    private class LazyScaleIntervals(IReadOnlyCollection<Note> notes) : LazyCollectionBase<Interval.Simple>(GetAll(notes))
    {
        private static IEnumerable<Interval.Simple> GetAll(IReadOnlyCollection<Note> notes)
        {
            var startNote = notes.ElementAt(0);
            var result = 
                notes
                    .Select(endNote => startNote.GetInterval(endNote))
                    .ToImmutableSortedSet()
                    .AsPrintable(Interval.Diatonic.Format.ShortName);

            return result;
        }
    }

    #endregion
}