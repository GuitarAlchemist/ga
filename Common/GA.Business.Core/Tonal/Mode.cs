namespace GA.Business.Core.Tonal;

using System.Collections.Immutable;
using System.ComponentModel;

using GA.Core;
using Intervals;
using Primitives;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Mode
{
    public abstract IReadOnlyCollection<Interval.Simple> Intervals { get; }
    public ModeQualities Qualities => new (this);

    protected abstract ModalScaleDegree ScaleDegree { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="Degree">The <see cref="MajorScaleDegree"/></param>
    /// <remarks>
    /// Mnemonic : I Don’t Particularly Like Modes A Lot
    /// </remarks>
    [PublicAPI]
    public sealed partial record MajorScale(MajorScaleDegree Degree) : Mode
    {
        public static MajorScale Ionian => new(1);
        public static MajorScale Dorian => new(2);
        public static MajorScale Phrygian => new(3);
        public static MajorScale Lydian => new(4);
        public static MajorScale Mixolydian => new(5);
        public static MajorScale Aeolian => new(6);
        public static MajorScale Locrian => new(7);

        public override IReadOnlyCollection<Interval.Simple> Intervals => IntervalsIndexer.Get(Degree);

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};

        #region region Intervals

        private class IntervalsIndexer : LazyIndexerBase<MajorScaleDegree, IReadOnlyCollection<Interval.Simple>>
        {
            private static readonly IntervalsIndexer _instance = new();
            public static IReadOnlyCollection<Interval.Simple> Get(MajorScaleDegree degree) => _instance[degree];

            public IntervalsIndexer() 
                : base(GetKeyValuePairs())
            {
            }

            private static IEnumerable<KeyValuePair<MajorScaleDegree, IReadOnlyCollection<Interval.Simple>>> GetKeyValuePairs()
            {
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var degree in MajorScaleDegree.All)
                {
                    var intervals = GetIntervals(degree).AsPrintable();
                    yield return new(degree, intervals);
                }

                static IReadOnlyCollection<Interval.Simple> GetIntervals(MajorScaleDegree degree)
                {
                    var notes = Key.Major.C.GetNotes().Rotate(degree.Value - 1);
                    var startNote = notes[0].ToAccidentedNote();
                    var result = 
                        notes.Select(note => startNote.GetInterval(note))
                             .ToImmutableList()
                             .AsPrintable(Interval.Simple.Format.AccidentedName);
                    return result;
                }
            }
        }

        #endregion
    }

    [PublicAPI]
    public sealed partial record MinorScale(MinorScaleDegree Degree) : Mode
    {
        public static MinorScale Aeolian => new(1);
        public static MinorScale Locrian => new(2);
        public static MinorScale Ionian => new(3);
        public static MinorScale Dorian => new(4);
        public static MinorScale Phrygian => new(5);
        public static MinorScale Lydian => new(6);
        public static MinorScale Mixolydian => new(7);

        public override IReadOnlyCollection<Interval.Simple> Intervals => IntervalsIndexer.Get(Degree);

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};

        #region region Intervals

        private class IntervalsIndexer : LazyIndexerBase<MinorScaleDegree, IReadOnlyCollection<Interval.Simple>>
        {
            private static readonly IntervalsIndexer _instance = new();
            public static IReadOnlyCollection<Interval.Simple> Get(MinorScaleDegree degree) => _instance[degree];

            public IntervalsIndexer() 
                : base(GetKeyValuePairs())
            {
            }

            private static IEnumerable<KeyValuePair<MinorScaleDegree, IReadOnlyCollection<Interval.Simple>>> GetKeyValuePairs()
            {
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var degree in MinorScaleDegree.All)
                {
                    var intervals = GetIntervals(degree).AsPrintable();
                    yield return new(degree, intervals);
                }

                static IReadOnlyCollection<Interval.Simple> GetIntervals(MinorScaleDegree degree)
                {
                    var notes = Key.Minor.A.GetNotes().Rotate(degree.Value - 1);
                    var startNote = notes[0].ToAccidentedNote();
                    var result = 
                        notes.Select(note => startNote.GetInterval(note))
                            .ToImmutableList()
                            .AsPrintable(Interval.Simple.Format.AccidentedName);

                    return result;
                }
            }
        }

        #endregion
    }

    [PublicAPI]
    public sealed partial record HarmonicMinorMode : Mode
    {
        [Description("Harmonic minor")]
        public static HarmonicMinorMode HarmonicMinor => new();
        [Description("locrian \u266E6")]
        public static HarmonicMinorMode LocrianNaturalSixth => new();
        [Description("Ionian augmented")]
        public static HarmonicMinorMode IonianAugmented => new();
        [Description("Dorian \u266F4")]
        public static HarmonicMinorMode DorianSharpFourth => new();
        [Description("Phrygian dominant")]
        public static HarmonicMinorMode PhrygianDominant => new();
        [Description("lydian \u266F2")]
        public static HarmonicMinorMode LydianSharpSecond => new();
        [Description("altered bb7")]
        public static HarmonicMinorMode Alteredd7 => new();

        public ModalScaleDegree Degree { get; init; }
        public override IReadOnlyCollection<Interval.Simple> Intervals => ImmutableList.Create<Interval.Simple>(); // TODO

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};
    }

    [PublicAPI]
    public sealed partial record MelodicMinorMode : Mode
    {
        [Description("Melodic minor")]
        public static MelodicMinorMode MelodicMinor => new();
        [Description("Dorian \u266D2")]
        public static MelodicMinorMode DorianFlatSecond => new();
        [Description("Lydian \u266F5")]
        public static MelodicMinorMode LydianAugmented => new();
        [Description("Lydian dominant")]
        public static MelodicMinorMode LydianDominant => new();
        [Description("Mixolydian \u266D6")]
        public static MelodicMinorMode MixolydianFlatSixth => new();
        [Description("Locrian \u266E2")]
        public static MelodicMinorMode LocrianNaturalSecond => new();
        [Description("Altered")]
        public static MelodicMinorMode Altered => new();

        public ModalScaleDegree Degree { get; init; }
        public override IReadOnlyCollection<Interval.Simple> Intervals => ImmutableList.Create<Interval.Simple>();

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};
    }
}