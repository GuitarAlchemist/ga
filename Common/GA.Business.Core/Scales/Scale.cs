namespace GA.Business.Core.Scales;

using Intervals;
using Notes;
using Tonal;
using GA.Core;
using static Notes.Note.AccidentedNote;
using Atonal;

/// <summary>
/// A music scale
/// </summary>
/// <remarks>
/// See https://www.youtube.com/c/TheExcitingUniverseofMusicTheory/videos
/// http://allthescales.org/
/// https://ianring.com/musictheory/
/// https://ianring.com/musictheory/scales/
/// https://chromatone.center/theory/scales/study.html
/// </remarks>
public class Scale : IReadOnlyCollection<Note>
{
    public static Scale Major => new(Key.Major.C.GetNotes());
    public static Scale NaturalMinor => Minor.Natural;
    public static Scale HarmonicMinor => Minor.Harmonic;
    public static Scale MelodicMinor => Minor.Melodic;
    public static Scale MajorPentatonic => new(C, D, E, G, A);

    /// <summary>
    /// https://ianring.com/musictheory/scales/1365
    /// </summary>
    public static Scale WholeTone => new(C, D, E, FSharp, GSharp, ASharp);
    public static Scale ChromaticSharp => new(C, CSharp, D, DSharp, E, F, FSharp, G, GSharp, A, ASharp);
    public static Scale ChromaticFlat => new(C, Db, D, Eb, E, F, Gb, G, Ab, A, Bb);


    public Scale(params Note.AccidentedNote[] notes) 
        : this(notes.AsEnumerable())
    {
    }

    public Scale(IEnumerable<Note> notes)
    {
        if (notes == null) throw new ArgumentNullException(nameof(notes));
        Notes = notes.ToImmutableList().AsPrintable();
        Intervals = new LazyScaleIntervals(Notes);
        Identity = PitchClassSetIdentity.FromNotes(Notes);
    }

    private IReadOnlyCollection<Note> Notes { get; }
    public IReadOnlyCollection<Interval.Simple> Intervals { get; }
    public PitchClassSetIdentity Identity { get; }

    public IEnumerator<Note> GetEnumerator() => Notes.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) Notes).GetEnumerator();
    public int Count => Notes.Count;

    public override string ToString() => Identity.ScaleName;

    #region Inner classes

    public static class Minor
    {
        public static Scale Natural => new(Key.Major.A.GetNotes());
        public static Scale Harmonic => new(A, B, C, D, E, F, GSharp);
        public static Scale Melodic => new(A, B, C, D, E, FSharp, GSharp);
    }

    private class LazyScaleIntervals : LazyCollectionBase<Interval.Simple>
    {
        public LazyScaleIntervals(IReadOnlyCollection<Note> notes) 
            : base(GetAll(notes))
        {
        }

        private static IEnumerable<Interval.Simple> GetAll(IReadOnlyCollection<Note> notes)
        {
            var startNote = notes.ElementAt(0);
            var result = 
                notes
                    .Select(endNote => startNote.GetInterval(endNote))
                    .ToImmutableList()
                    .AsPrintable(Interval.Simple.Format.ShortName);

            return result;
        }
    }

    #endregion
}