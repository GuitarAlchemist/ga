namespace GA.Business.Core.Tonal;

using Scales;
using Intervals;
using Primitives;

/// <remarks>
/// https://www.pianoscales.org/major.html
/// https://www.pianoscales.org/minor.html#natural
/// https://www.pianoscales.org/minor-harmonic.html
/// https://www.pianoscales.org/minor-melodic.html
/// TODO: Add support for chords - Making chords from scales http://www.ethanhein.com/wp/2015/making-chords-from-scales/
/// </remarks>
[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Mode
{
    public abstract string Name { get; }
    public abstract IReadOnlyCollection<Interval.Simple> Intervals { get; }
    public bool IsMinorMode => Intervals.Contains(Interval.Simple.MinorThird);
    public ModeFormula Formula => new (this);
    public Mode RefMode => IsMinorMode ? Major.Aeolian : Major.Ionian;

    protected abstract ModalScaleDegree ScaleDegree { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="Degree">The <see cref="MajorScaleDegree"/></param>
    /// <remarks>
    /// Mnemonic : I Don’t Particularly Like Modes A Lot
    /// </remarks>
    [PublicAPI]
    public sealed partial record Major(MajorScaleDegree Degree) : Mode
    {
        public static Major Ionian => new(1);
        public static Major Dorian => new(2);
        public static Major Phrygian => new(3);
        public static Major Lydian => new(4);
        public static Major Mixolydian => new(5);
        public static Major Aeolian => new(6);
        public static Major Locrian => new(7);

        public override string Name => Degree.Value switch
        {
            1 => nameof(Ionian),
            2 => nameof(Dorian),
            3 => nameof(Phrygian),
            4 => nameof(Lydian),
            5 => nameof(Mixolydian),
            6 => nameof(Aeolian),
            7 => nameof(Locrian),
            _ => throw new ArgumentOutOfRangeException(nameof(Degree))
        };
            
        public override IReadOnlyCollection<Interval.Simple> Intervals => ModeIntervalsByDegree.Instance[ScaleDegree];

        public override string ToString() => Formula.ToString();

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};

        private class ModeIntervalsByDegree : ModeIntervalsByDegreeBase
        {
            public static readonly ModeIntervalsByDegree Instance = new();
            public ModeIntervalsByDegree() : base(Scale.Major) { }
        }
    }

    [PublicAPI]
    public sealed partial record NaturalMinor(MinorScaleDegree Degree) : Mode
    {
        public static NaturalMinor Aeolian => new(1);
        public static NaturalMinor Locrian => new(2);
        public static NaturalMinor Ionian => new(3);
        public static NaturalMinor Dorian => new(4);
        public static NaturalMinor Phrygian => new(5);
        public static NaturalMinor Lydian => new(6);
        public static NaturalMinor Mixolydian => new(7);

        public override string Name => Degree.Value switch
        {
            1 => nameof(Aeolian),
            2 => nameof(Locrian),
            3 => nameof(Ionian),
            4 => nameof(Dorian),
            5 => nameof(Phrygian),
            6 => nameof(Lydian),
            7 => nameof(Mixolydian),
            _ => throw new ArgumentOutOfRangeException(nameof(Degree))
        };

        public override IReadOnlyCollection<Interval.Simple> Intervals => ModeIntervalsByDegree.Instance[ScaleDegree];

        public override string ToString() => Formula.ToString();

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};

        private class ModeIntervalsByDegree : ModeIntervalsByDegreeBase
        {
            public static readonly ModeIntervalsByDegree Instance = new();
            public ModeIntervalsByDegree() : base(Scale.NaturalMinor) { }
        }
    }

    [PublicAPI]
    public sealed partial record HarmonicMinor(ModalScaleDegree Degree) : Mode
    {
        public static HarmonicMinor HarmonicMinorScale => new(1);
        public static HarmonicMinor LocrianNaturalSixth => new(2);
        public static HarmonicMinor IonianAugmented => new(3);
        public static HarmonicMinor DorianSharpFourth => new(4);
        public static HarmonicMinor PhrygianDominant => new(5);
        public static HarmonicMinor LydianSharpSecond => new(6);
        public static HarmonicMinor Alteredd7 => new(7);

        public override string Name => Degree.Value switch
        {
            1 => "Harmonic minor",
            2 => "locrian \u266E6",
            3 => "Ionian augmented",
            4 => "Dorian \u266F4",
            5 => "Phrygian dominant",
            6 => "lydian \u266F2",
            7 => "altered bb7",
            _ => throw new ArgumentOutOfRangeException(nameof(Degree))
        };
        public override IReadOnlyCollection<Interval.Simple> Intervals => ModeIntervalsByDegree.Instance[Degree];

        public override string ToString() => Formula.ToString();

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};

        private class ModeIntervalsByDegree : ModeIntervalsByDegreeBase
        {
            public static readonly ModeIntervalsByDegree Instance = new();
            public ModeIntervalsByDegree() : base(Scale.HarmonicMinor) { }
        }
    }

    [PublicAPI]
    public sealed partial record MelodicMinorMode(ModalScaleDegree Degree) : Mode
    {
        public static MelodicMinorMode MelodicMinor => new(1);
        public static MelodicMinorMode DorianFlatSecond => new(2);
        public static MelodicMinorMode LydianAugmented => new(3);
        public static MelodicMinorMode LydianDominant => new(4);
        public static MelodicMinorMode MixolydianFlatSixth => new(5);
        public static MelodicMinorMode LocrianNaturalSecond => new(6);
        public static MelodicMinorMode Altered => new(7);

        public override string Name => Degree.Value switch
        {
            1 => "Melodic minor",
            2 => "Dorian \u266D2",
            3 => "Lydian \u266F5",
            4 => "Lydian dominant",
            5 => "Mixolydian \u266D6",
            6 => "Locrian \u266E2",
            7 => "Altered",
            _ => throw new ArgumentOutOfRangeException(nameof(Degree))
        };

        public override IReadOnlyCollection<Interval.Simple> Intervals => ModeIntervalsByDegree.Instance[Degree];

        protected override ModalScaleDegree ScaleDegree => new() {Value = Degree.Value};

        private class ModeIntervalsByDegree : ModeIntervalsByDegreeBase
        {
            public static readonly ModeIntervalsByDegree Instance = new();
            public ModeIntervalsByDegree() : base(Scale.MelodicMinor) { }
        }
    }
}