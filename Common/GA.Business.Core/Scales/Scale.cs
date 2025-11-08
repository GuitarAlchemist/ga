namespace GA.Business.Core.Scales;

using Atonal;
using Atonal.Primitives;
using GA.Core.Extensions;
using Intervals;
using Notes;

/// <summary>
///     A scale
/// </summary>
/// <remarks>
///     See https://www.youtube.com/c/TheExcitingUniverseofmusictheory/videos
///     http://allthescales.org/
///     https://ianring.com/musictheory/
///     https://ianring.com/musictheory/scales/
///     https://chromatone.center/theory/scales/study.html
/// </remarks>
public class Scale : IStaticReadonlyCollection<Scale>,
    IReadOnlyCollection<Note>
{
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

    // Diatonic scales
    public static Scale Major => new("C D E F G A B");
    public static Scale NaturalMinor => Minor.Natural;
    public static Scale HarmonicMinor => Minor.Harmonic;
    public static Scale MelodicMinor => Minor.Melodic;
    public static Scale MajorPentatonic => new("C D E G A");

    // Symmetric scales
    public static Scale WholeTone => new("C D E F# G# A#");
    public static Scale Augmented => new("C D# E G G# B");
    public static Scale Diminished => new("C D Eb F Gb Ab A B");

    // Other scales
    public static Scale Blues => new("C Eb F F# G Bb");
    public static Scale BebopDominant => new("C D E F G A Bb B");
    public static Scale Tritone => new("C Db E F# G A");
    public static Scale DoubleHarmonic => new("C Db E F G Ab B");
    public static Scale Enigmatic => new("C Db E F# G# A# B");
    public static Scale Prometheus => new("C D E F# A Bb");
    public static Scale HarmonicMajor => new("C D E F G Ab B");
    public static Scale NeapolitanMinor => new("C Db Eb F G Ab Bb");
    public static Scale NeapolitanMajor => new("C Db Eb F G A B");
    public static Scale JapaneseHirajoshi => new("C D Eb G Ab");
    public static Scale InSen => new("C Db F G Bb");

    public IReadOnlyCollection<Interval.Simple> Intervals { get; }
    public PitchClassSet PitchClassSet { get; }
    public IntervalClassVector IntervalClassVector { get; }
    public bool IsModal => PitchClassSet.IsModal;

    public ModalFamily? ModalFamily =>
        ModalFamily.TryGetValue(IntervalClassVector, out var modalFamily) ? modalFamily : null;

    public IEnumerator<Note> GetEnumerator()
    {
        return _notes.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)_notes).GetEnumerator();
    }

    public int Count => _notes.Count;

    #region IStaticReadonlyCollection<Scale> Members

    public static IReadOnlyCollection<Scale> Items =>
        PitchClassSet.Items
            .Where(set => set.Id.IsScale)
            .Select(set => FromPitchClassSetId(set.Id))
            .ToLazyCollection();

    #endregion

    public static Scale FromPitchClassSetId(PitchClassSetId id)
    {
        return new Scale(id.Notes);
    }

    public override string ToString()
    {
        var scaleName = string.Empty; // TODO
        if (string.IsNullOrEmpty(scaleName))
        {
            return _notes.ToString()!;
        }

        return $"{scaleName} - {_notes}";
    }

    #region Inner classes

    public static class Minor
    {
        public static Scale Natural => new("A B C D E F G");
        public static Scale Harmonic => new("A B C D E F G#");
        public static Scale Melodic => new("A B C D E F# G#");
    }

    private class LazyScaleIntervals(IReadOnlyCollection<Note> notes)
        : LazyCollectionBase<Interval.Simple>(GetCollection(notes))
    {
        private static IEnumerable<Interval.Simple> GetCollection(IReadOnlyCollection<Note> notes)
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
