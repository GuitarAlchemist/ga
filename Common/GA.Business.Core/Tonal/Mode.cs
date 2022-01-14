using System.ComponentModel;
using GA.Business.Core.Tonal.Primitives;

namespace GA.Business.Core.Tonal;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Mode()
{
    protected abstract int ScaleDegree { get; }

    public sealed partial record Major(MajorScaleDegree Degree) : Mode
    {
        public static Major Ionian => new(1);
        public static Major Dorian => new(2);
        public static Major Phrygian => new(3);
        public static Major Lydian => new(4);
        public static Major Mixolydian => new(5);
        public static Major Aeolian => new(6);
        public static Major Locrian => new(7);

        protected override int ScaleDegree => Degree;
    }

    public sealed partial record NaturalMinor(MinorScaleDegree Degree) : Mode
    {
        public static NaturalMinor Aeolian => new(1);
        public static NaturalMinor Locrian => new(2);
        public static NaturalMinor Ionian => new(3);
        public static NaturalMinor Dorian => new(4);
        public static NaturalMinor Phrygian => new(5);
        public static NaturalMinor Lydian => new(6);
        public static NaturalMinor Mixolydian => new(7);

        protected override int ScaleDegree => Degree;
    }

    public sealed partial record Harmonic() : Mode
    {
        [Description("Harmonic minor")]
        public static Harmonic HarmonicMinor => new();
        [Description("locrian \u266E6")]
        public static Harmonic LocrianNaturalSixth => new();
        [Description("Ionian augmented")]
        public static Harmonic IonianAugmented => new();
        [Description("Dorian \u266F4")]
        public static Harmonic DorianSharpFourth => new();
        [Description("Phrygian dominant")]
        public static Harmonic PhrygianDominant => new();
        [Description("lydian \u266F2")]
        public static Harmonic LydianSharpSecond => new();
        [Description("altered bb7")]
        public static Harmonic Alteredd7 => new();

        public ModalScaleDegree Degree { get; init; }

        protected override int ScaleDegree => Degree;
    }

    public sealed partial record Melodic : Mode
    {
        [Description("Melodic minor")]
        public static Melodic MelodicMinor => new();
        [Description("Dorian \u266D2")]
        public static Melodic DorianFlatSecond => new();
        [Description("Lydian \u266F5")]
        public static Melodic LydianAugmented => new();
        [Description("Lydian dominant")]
        public static Melodic LydianDominant => new();
        [Description("Mixolydian \u266D6")]
        public static Melodic MixolydianFlatSixth => new();
        [Description("Locrian \u266E2")]
        public static Melodic LocrianNaturalSecond => new();
        [Description("Altered")]
        public static Melodic Altered => new();

        public ModalScaleDegree Degree { get; init; }

        protected override int ScaleDegree => Degree;
    }

}