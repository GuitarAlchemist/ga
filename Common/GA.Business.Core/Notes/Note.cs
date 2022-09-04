
namespace GA.Business.Core.Notes;

using GA.Core;
using GA.Business.Core.Intervals.Primitives;
using Tonal;
using Intervals;
using Primitives;
using static Primitives.SharpAccidental;
using static Primitives.FlatAccidental;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Note : IComparable<Note>
{
    #region Relational Comparers

    public int CompareTo(Note? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return PitchClass.CompareTo(other.PitchClass);
    }

    public static bool operator <(Note? left, Note? right) => Comparer<Note>.Default.Compare(left, right) < 0;
    public static bool operator >(Note? left, Note? right) => Comparer<Note>.Default.Compare(left, right) > 0;
    public static bool operator <=(Note? left, Note? right) => Comparer<Note>.Default.Compare(left, right) <= 0;
    public static bool operator >=(Note? left, Note? right) => Comparer<Note>.Default.Compare(left, right) >= 0;

    #endregion

    /// <summary>
    /// Gets the <see cref="PitchClass"/>.
    /// </summary>
    public abstract PitchClass PitchClass { get; }

    public abstract AccidentedNote ToAccidentedNote();

    public virtual Interval.Simple GetInterval(Note other)
    {
        var startNote = ToAccidentedNote();
        var endNote = other.ToAccidentedNote();
        return startNote.GetInterval(endNote);
    }

    /// <summary>
    /// Gets the shortest distance between two notes.
    /// </summary>
    /// <remarks>See https://en.wikipedia.org/wiki/Interval_class</remarks>
    /// <param name="other">The other <see cref="Note"/></param>
    /// <returns>The <see cref="Semitones"/> distance</returns>
    public Semitones GetIntervalClass(Note other)
    {
        var dist1 = (GetInterval(other).ToSemitones() +12) % 12;
        var dist2 = (other.GetInterval(this).ToSemitones() + 12) % 12;
        return dist1 <= dist2 ? dist1 : dist2;
    }

    public static IReadOnlyCollection<SharpKey> AllSharp => SharpKey.All;
    public static IReadOnlyCollection<FlatKey> AllFlat => FlatKey.All;

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A chromatic note.
    /// </summary>
    [PublicAPI]
    public sealed partial record Chromatic(PitchClass PitchClass) : Note
    {
        public static Chromatic C => new(0);
        public static Chromatic CSharpDb => new(1);
        public static Chromatic D => new(2);
        public static Chromatic DSharpEb => new(3);
        public static Chromatic E => new(4);
        public static Chromatic F => new(5);
        public static Chromatic FSharpGb => new(6);
        public static Chromatic G => new(7);
        public static Chromatic GSharpAb => new(8);
        public static Chromatic A => new(9);
        public static Chromatic ASharpBb => new(10);
        public static Chromatic B => new(11);

        public override PitchClass PitchClass { get; } = PitchClass;
        public override AccidentedNote ToAccidentedNote() => ToSharp().ToAccidentedNote();
        public SharpKey ToSharp() => PitchClass.ToSharpNote();
        public FlatKey ToFlat() => PitchClass.ToFlatNote();

        public static implicit operator Chromatic(NaturalNote naturalNote) => new(naturalNote.ToPitchClass());
        public static Interval.Chromatic operator -(Chromatic note1, Chromatic note2) => note1.PitchClass - note2.PitchClass;

        public override string ToString()
        {
            var sharp = ToSharp();
            var flat = ToFlat();
            return sharp.SharpAccidental.HasValue ? $"{sharp}/{flat}" : $"{sharp}";
        }
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A note from a musical key (Sharp or flat key).
    /// </summary>
    /// <param name="NaturalNote"></param>
    [PublicAPI]
    public abstract record KeyNote(NaturalNote NaturalNote) : Note, IComparable<KeyNote>
    {
        #region Relational Members

        public int CompareTo(KeyNote? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var naturalNoteComparison = NaturalNote.CompareTo(other.NaturalNote);
            if (naturalNoteComparison != 0) return naturalNoteComparison;
            return Nullable.Compare(Accidental, other.Accidental);
        }

        public static bool operator <(KeyNote? left, KeyNote? right) => Comparer<KeyNote>.Default.Compare(left, right) < 0;
        public static bool operator >(KeyNote? left, KeyNote? right) => Comparer<KeyNote>.Default.Compare(left, right) > 0;
        public static bool operator <=(KeyNote? left, KeyNote? right) => Comparer<KeyNote>.Default.Compare(left, right) <= 0;
        public static bool operator >=(KeyNote? left, KeyNote? right) => Comparer<KeyNote>.Default.Compare(left, right) >= 0;

        #endregion

        public abstract AccidentalKind AccidentalKind { get; }
        public abstract Accidental? Accidental { get; }
        public override PitchClass PitchClass => GetPitchClass();
        public override AccidentedNote ToAccidentedNote() => new(NaturalNote, Accidental);
        public Chromatic ToChromaticNote() => new(PitchClass);

        protected abstract PitchClass GetPitchClass();
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A note from a sharp musical key.
    /// </summary>
    [PublicAPI]
    public sealed partial record SharpKey(NaturalNote NaturalNote, SharpAccidental? SharpAccidental = null) : KeyNote(NaturalNote)
    {
        public static SharpKey C => new(NaturalNote.C);
        public static SharpKey CSharp => new(NaturalNote.C, Sharp);
        public static SharpKey D => new(NaturalNote.D);
        public static SharpKey DSharp => new(NaturalNote.D, Sharp);
        public static SharpKey E => new(NaturalNote.E);
        public static SharpKey F => new(NaturalNote.F);
        public static SharpKey FSharp => new(NaturalNote.F, Sharp);
        public static SharpKey G => new(NaturalNote.G);
        public static SharpKey GSharp => new(NaturalNote.G, Sharp);
        public static SharpKey A => new(NaturalNote.A);
        public static SharpKey ASharp => new(NaturalNote.A, Sharp);
        public static SharpKey B => new(NaturalNote.B);

        public static implicit operator Chromatic(SharpKey sharpKey) => new(sharpKey.PitchClass);

        public static IReadOnlyCollection<SharpKey> All => new[] { C, CSharp, D, DSharp, E, F, FSharp, G, GSharp, A, ASharp, B }.ToImmutableList();
        public static IReadOnlyCollection<SharpKey> Natural => new[] { C, D, E, F, G, A, B }.ToImmutableList();

        public override AccidentalKind AccidentalKind => AccidentalKind.Sharp;
        public override Accidental? Accidental => SharpAccidental;

        public override string ToString() =>
            SharpAccidental.HasValue
                ? $"{NaturalNote}{SharpAccidental.Value}"
                : $"{NaturalNote}";

        protected override PitchClass GetPitchClass()
        {
            var value = NaturalNote.ToPitchClass().Value;
            if (SharpAccidental.HasValue) value += SharpAccidental.Value.Value;
            var result = new PitchClass { Value = value };

            return result;
        }
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A note from a flat musical key.
    /// </summary>
    [PublicAPI]
    public sealed partial record FlatKey(NaturalNote NaturalNote, FlatAccidental? FlatAccidental = null) : KeyNote(NaturalNote)
    {
        public static FlatKey CFlat => new(NaturalNote.C, Flat);
        public static FlatKey C => new(NaturalNote.C);
        public static FlatKey DFlat => new(NaturalNote.D, Flat);
        public static FlatKey D => new(NaturalNote.D);
        public static FlatKey EFlat => new(NaturalNote.E, Flat);
        public static FlatKey E => new(NaturalNote.E);
        public static FlatKey FFlat => new(NaturalNote.F, Flat);
        public static FlatKey F => new(NaturalNote.F);
        public static FlatKey GFlat => new(NaturalNote.G, Flat);
        public static FlatKey G => new(NaturalNote.G);
        public static FlatKey AFlat => new(NaturalNote.A, Flat);
        public static FlatKey A => new(NaturalNote.A);
        public static FlatKey BFlat => new(NaturalNote.B, Flat);
        public static FlatKey B => new(NaturalNote.B);

        public static IReadOnlyCollection<FlatKey> All => new[] { C, DFlat, D, EFlat, E, F, GFlat, G, AFlat, A, BFlat, B }.ToImmutableList();

        public static implicit operator Chromatic(FlatKey flatKeyNote) => new(flatKeyNote.PitchClass);

        public override PitchClass PitchClass => GetPitchClass();
        public override AccidentalKind AccidentalKind => AccidentalKind.Flat;
        public override Accidental? Accidental => FlatAccidental;

        public override string ToString() =>
            FlatAccidental.HasValue
                ? $"{NaturalNote}{FlatAccidental.Value}"
                : $"{NaturalNote}";

        protected override PitchClass GetPitchClass()
        {
            var value = NaturalNote.ToPitchClass().Value;
            if (FlatAccidental.HasValue) value += FlatAccidental.Value.Value;
            var result = new PitchClass { Value = value };

            return result;
        }
    }

    /// <inheritdoc cref="Note"/>
    /// <summary>
    /// A note with an optional accidental
    /// </summary>
    [PublicAPI]
    public sealed partial record AccidentedNote(NaturalNote NaturalNote, Accidental? Accidental = null) : Note
    {
        public static implicit operator Chromatic(AccidentedNote accidentedNote) => new(accidentedNote.PitchClass);
        public static implicit operator AccidentedNote(KeyNote keyNote) => new(keyNote.NaturalNote, keyNote.Accidental);
        public static Interval.Simple operator -(AccidentedNote note1, AccidentedNote note2) => note1.GetInterval(note2);
        public static IReadOnlyCollection<AccidentedNote> All => AllNotes.Instance;

        public static AccidentedNote C => new(NaturalNote.C);
        public static AccidentedNote Cb => new(NaturalNote.C, Flat);
        public static AccidentedNote CSharp => new(NaturalNote.C, Sharp);
        public static AccidentedNote D => new(NaturalNote.D);
        public static AccidentedNote Db => new(NaturalNote.D, Flat);
        public static AccidentedNote DSharp => new(NaturalNote.D, Sharp);
        public static AccidentedNote E => new(NaturalNote.E);
        public static AccidentedNote Eb => new(NaturalNote.E, Flat);
        public static AccidentedNote ESharp => new(NaturalNote.E, Sharp);
        public static AccidentedNote F => new(NaturalNote.F);
        public static AccidentedNote Fb => new(NaturalNote.F, Flat);
        public static AccidentedNote FSharp => new(NaturalNote.F, Sharp);
        public static AccidentedNote G => new(NaturalNote.G);
        public static AccidentedNote Gb => new(NaturalNote.G, Flat);
        public static AccidentedNote GSharp => new(NaturalNote.G, Sharp);
        public static AccidentedNote A => new(NaturalNote.A);
        public static AccidentedNote Ab => new(NaturalNote.A, Flat);
        public static AccidentedNote ASharp => new(NaturalNote.A, Sharp);
        public static AccidentedNote B => new(NaturalNote.B);
        public static AccidentedNote Bb => new(NaturalNote.B, Flat);
        public static AccidentedNote BSharp => new(NaturalNote.B, Sharp);

        public override PitchClass PitchClass => GetPitchClass();
        public override AccidentedNote ToAccidentedNote() => this;
        public override Interval.Simple GetInterval(Note other) => GetInterval(other.ToAccidentedNote());
        // public Interval.Simple GetInterval(AccidentedNote other) => NoteIntervalsIndexer.Get(this, other);
        public Interval.Simple GetInterval(AccidentedNote other) => GetInterval(this, other);

        public override string ToString() => $"{NaturalNote}{Accidental}";

        private PitchClass GetPitchClass()
        {
            var value = NaturalNote.ToPitchClass().Value;
            if (Accidental.HasValue) value += Accidental.Value.Value;
            var result = new PitchClass { Value = value };

            return result;
        }

        private static Interval.Simple GetInterval(
            AccidentedNote startNote,
            AccidentedNote endNote)
        {
            if (startNote == endNote) return Interval.Simple.Unison;

            var key = Key.FromRoot(startNote.NaturalNote);
            var size = endNote.NaturalNote - startNote.NaturalNote;
            var qualityIncrement = GetQualityIncrement(key, startNote, endNote);
            var quality = GetQuality(size, qualityIncrement);
            var result = new Interval.Simple
            {
                Size = size,
                Quality = quality
            };

            return result;

            static Semitones GetQualityIncrement(
                Key key, 
                AccidentedNote startNote,
                AccidentedNote endNote)
            {
                var result = Semitones.None;

                // Quality - Start note
                if (startNote.Accidental.HasValue) result -= startNote.Accidental.Value.ToSemitones();

                // Quality - End note
                var (endNaturalNote, endNoteAccidental) = endNote;
                if (key.KeySignature.Contains(endNaturalNote))
                {
                    var expectedEndNoteAccidental =
                        key.AccidentalKind == AccidentalKind.Flat
                            ? Intervals.Accidental.Flat
                            : Intervals.Accidental.Sharp;

                    if (endNoteAccidental == expectedEndNoteAccidental) return result;

                    var actualEndNoteAccidentalValue = endNoteAccidental?.Value ?? 0;
                    var endNoteAccidentalDelta = actualEndNoteAccidentalValue - expectedEndNoteAccidental.Value;
                    result += endNoteAccidentalDelta;
                }
                else if (endNoteAccidental.HasValue) result += endNoteAccidental.Value.ToSemitones();

                return result;
            }

            static IntervalQuality GetQuality(
                IntervalSize number,
                Semitones qualityIncrement)
            {
                if (number.Consonance == IntervalSizeConsonance.Perfect)
                {
                    var result = qualityIncrement.Value switch
                    {
                        -2 => IntervalQuality.DoublyDiminished,
                        -1 => IntervalQuality.Diminished,
                        0 => IntervalQuality.Perfect,
                        1 => IntervalQuality.Augmented,
                        2 => IntervalQuality.DoublyAugmented,
                        _ => throw new NotSupportedException()
                    };

                    return result;
                }
                else
                {
                    var result = qualityIncrement.Value switch
                    {
                        -3 => IntervalQuality.DoublyDiminished,
                        -2 => IntervalQuality.Diminished,
                        -1 => IntervalQuality.Minor,
                        0 => IntervalQuality.Major,
                        1 => IntervalQuality.Augmented,
                        2 => IntervalQuality.DoublyAugmented,
                        _ => throw new NotSupportedException()
                    };

                    return result;
                }
            }
        }

        private class AllNotes : LazyCollectionBase<AccidentedNote>
        {
            public static readonly AllNotes Instance = new();

            public AllNotes()
                : base(GetAll())
            {
            }

            private static IEnumerable<AccidentedNote> GetAll()
            {
                var accidentals = new[]
                {
                    Intervals.Accidental.DoubleFlat,
                    Intervals.Accidental.Flat,
                    Intervals.Accidental.Natural,
                    Intervals.Accidental.Sharp,
                    Intervals.Accidental.DoubleSharp,
                };

                foreach (var naturalNote in NaturalNote.Items)
                    foreach (var accidental in accidentals)
                        yield return new(naturalNote, accidental);
            }
        }
    }
}