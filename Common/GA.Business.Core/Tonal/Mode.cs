namespace GA.Business.Core.Tonal;

using System.ComponentModel;

using Intervals;
using Primitives;


[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Mode
{
    protected abstract ModalScaleDegree ScaleDegree { get; }

    public sealed partial record MajorMode(MajorScaleDegree Degree) : Mode
    {
        public static MajorMode Ionian => new(1);
        public static MajorMode Dorian => new(2);
        public static MajorMode Phrygian => new(3);
        public static MajorMode Lydian => new(4);
        public static MajorMode Mixolydian => new(5);
        public static MajorMode Aeolian => new(6);
        public static MajorMode Locrian => new(7);

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};

        public IEnumerable<Interval.Compound> GetIntervals(MajorScaleDegree startDegree)
        {
            return null; // TODO
        }
    }

    public sealed partial record NaturalMinorMode(MinorScaleDegree Degree) : Mode
    {
        public static NaturalMinorMode Aeolian => new(1);
        public static NaturalMinorMode Locrian => new(2);
        public static NaturalMinorMode Ionian => new(3);
        public static NaturalMinorMode Dorian => new(4);
        public static NaturalMinorMode Phrygian => new(5);
        public static NaturalMinorMode Lydian => new(6);
        public static NaturalMinorMode Mixolydian => new(7);

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};
    }

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

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};
    }

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

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};
    }
}