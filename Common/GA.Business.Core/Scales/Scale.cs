namespace GA.Business.Core.Scales;

using System.Collections;
using System.Collections.Immutable;

using Notes.Extensions;
using Intervals;
using Notes;
using Tonal;
using GA.Core;
using static Notes.Note.AccidentedNote;

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
    public static Scale NaturalMinor => new(Key.Major.A.GetNotes());
    public static Scale HarmonicMinor => new(A, B, C, D, E, F, GSharp);
    public static Scale MelodicMinor => new(A, B, C, D, E, FSharp, GSharp);
    public static Scale MajorPentatonic => new(C, D, E, G, A);

    /// <summary>
    /// https://ianring.com/musictheory/scales/1365
    /// </summary>
    public static Scale WholeTone => new(C, D, E, FSharp, GSharp, ASharp);

    private readonly IReadOnlyCollection<Note> _notes;
    private readonly LazyScaleIntervals _lazyScaleIntervals;

    public Scale(params Note.AccidentedNote[] notes) 
        : this(notes.AsEnumerable())
    {
    }

    public Scale(IEnumerable<Note> notes)
    {
        if (notes == null) throw new ArgumentNullException(nameof(notes));
        _notes = notes.ToImmutableList().AsPrintable();
        _lazyScaleIntervals = new(_notes);
    }

    public IReadOnlyCollection<Interval.Simple> Intervals => _lazyScaleIntervals;

    public IEnumerator<Note> GetEnumerator() => _notes.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _notes).GetEnumerator();
    public int Count => _notes.Count;
    public int ScaleNumber => _notes.ToPitchClassSet().GetIdentity();
    public ScaleIdentity ScaleIdentity => new(ScaleNumber);

    #region Inner classes

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