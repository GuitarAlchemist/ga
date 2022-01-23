namespace GA.Business.Core.Notes;

using System.Collections.Immutable;

using GA.Core;
using GA.Business.Core.Intervals.Primitives;
using Tonal;
using Intervals;
using Primitives;
using static Primitives.SharpAccidental;
using static Primitives.FlatAccidental;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Note
{
    /// <summary>
    /// Gets the <see cref="PitchClass"/>.
    /// </summary>
    public abstract PitchClass PitchClass { get; }

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
        public AccidentedNote ToAccidentedNote() => new(NaturalNote, Accidental);

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

        public override AccidentalKind AccidentalKind => AccidentalKind.Sharp;
        public override Accidental? Accidental => SharpAccidental;

        public override string ToString() =>
            SharpAccidental.HasValue
                ? $"{NaturalNote}{SharpAccidental.Value}"
                : $"{NaturalNote}";

        protected override PitchClass GetPitchClass()
        {
            var result = new PitchClass
            {
                Value = NaturalNote.ToPitchClass().Value + (SharpAccidental?.Value ?? 0)
            };

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
            var value = NaturalNote.ToPitchClass().Value + FlatAccidental?.Value ?? 0;
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

        public override PitchClass PitchClass => GetPitchClass();

        public override string ToString() => $"{NaturalNote}{Accidental}";

        public Interval.Simple GetInterval(AccidentedNote other) => SimpleIntervals.Get(this, other);

        private PitchClass GetPitchClass()
        {
            var result = new PitchClass
            {
                Value = NaturalNote.ToPitchClass().Value + (Accidental?.Value ?? 0)
            };

            return result;
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

                foreach (var naturalNote in NaturalNote.All)
                    foreach (var accidental in accidentals)
                        yield return new(naturalNote, accidental);
            }
        }

        private class SimpleIntervals : LazyIndexerBase<(AccidentedNote, AccidentedNote), Interval.Simple>
        {
            public static Interval.Simple Get(AccidentedNote note1, AccidentedNote note2) => _instance[(note1, note2)];

            private static readonly IReadOnlyDictionary<NaturalNote, Key> _keyByNaturalNote =
                new Dictionary<NaturalNote, Key>
                {
                    [NaturalNote.C] = Key.Major.C,
                    [NaturalNote.D] = Key.Major.D,
                    [NaturalNote.E] = Key.Major.E,
                    [NaturalNote.F] = Key.Major.F,
                    [NaturalNote.G] = Key.Major.G,
                    [NaturalNote.A] = Key.Major.A,
                    [NaturalNote.B] = Key.Major.B
                }.ToImmutableDictionary();

            private static readonly SimpleIntervals _instance = new();

            public SimpleIntervals()
                : base(GetKeyValuePairs())
            {
            }

            private static IEnumerable<KeyValuePair<(AccidentedNote, AccidentedNote), Interval.Simple>> GetKeyValuePairs()
            {
                var startNotes = new SortedSet<KeyNote>();
                startNotes.UnionWith(AllSharp);
                startNotes.UnionWith(AllFlat);

                foreach (var startNote in startNotes)
                foreach (var diatonicNumber in DiatonicNumber.All)
                {
                    var isPerfect = diatonicNumber.IsPerfect;
                    if (isPerfect)
                    {
                        yield return CreateItem(
                            startNote, 
                            diatonicNumber, 
                            Quality.Perfect, 
                            Quality.Augmented, 
                            Quality.Diminished);
                    }
                    else
                    {
                        yield return CreateItem(
                            startNote, 
                            diatonicNumber, 
                            Quality.Major,
                            Quality.Augmented,
                            Quality.Minor);
                    }
                }

                static KeyValuePair<(AccidentedNote, AccidentedNote), Interval.Simple> CreateItem(
                    KeyNote startNote,
                    DiatonicNumber diatonicNumber,
                    Quality quality,
                    Quality sharpQuality,
                    Quality flatQuality)
                {
                    // Compute end note
                    var endNote = new AccidentedNote(startNote.NaturalNote + diatonicNumber, quality.ToAccidental());

                    // Adjust quality for key accidental
                    var key = _keyByNaturalNote[startNote.NaturalNote];
                    if (key.IsNoteAccidental(endNote.NaturalNote))
                    {
                        switch (key.AccidentalKind)
                        {
                            case AccidentalKind.Sharp:
                                quality = flatQuality;
                                break;
                            case AccidentalKind.Flat:
                                quality = sharpQuality;
                                break;
                        }
                    }

                    // Create item
                    var itemKey = (note1: startNote, note2: endNote);
                    var itemValue = new Interval.Simple { Size = diatonicNumber, Quality = quality };
                    var item = new KeyValuePair<(AccidentedNote, AccidentedNote), Interval.Simple>(itemKey, itemValue);
                    return item;
                }
            }
        }

        /*
         
        TODO - Compound intervals

        private class CompoundIntervals : LazyIndexerBase<(AccidentedNote, AccidentedNote), Interval.Compound>
        {
            public static Interval.Compound Get(AccidentedNote note1, AccidentedNote note2) => _instance[(note1, note2)];

            private static readonly CompoundIntervals _instance = new();

            public CompoundIntervals()
                : base(GetKeyValuePairs())
            {
            }

            private static IEnumerable<KeyValuePair<(AccidentedNote, AccidentedNote), Interval.Compound>> GetKeyValuePairs()
            {
                var startNotes = new SortedSet<KeyNote>();
                startNotes.UnionWith(AllSharp);
                startNotes.UnionWith(AllFlat);

                foreach (var startNote in startNotes)
                foreach (var diatonicNumber in CompoundDiatonicNumber.All)
                foreach (var quality in diatonicNumber.ToSimple().AvailableQualities)
                {
                    var item = CreateItem(startNote, diatonicNumber, quality);
                    yield return item;
                }

                static KeyValuePair<(AccidentedNote, AccidentedNote), Interval.Compound> CreateItem(
                    KeyNote startNote, 
                    CompoundDiatonicNumber diatonicNumber, 
                    Quality quality)
                {
                    var endNote = new AccidentedNote(startNote.NaturalNote + diatonicNumber, quality.ToAccidental());
                    var key = (note1: startNote, note2: endNote);
                    var value = new Interval.Compound() {Size = diatonicNumber, Quality = quality};
                    var item = new KeyValuePair<(AccidentedNote, AccidentedNote), Interval.Compound>(key, value);
                    return item;
                }
            }
        */
    }
}