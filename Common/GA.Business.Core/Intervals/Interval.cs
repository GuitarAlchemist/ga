using GA.Business.Core.Intervals.Primitives;

namespace GA.Business.Core.Intervals;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Interval
{
    /// <inheritdoc cref="Interval"/>
    /// <summary>
    /// A chromatic interval
    /// </summary>
    /// <remarks>
    /// https://viva.pressbooks.pub/openmusictheory/chapter/intervals-in-integer-notation/
    /// </remarks>
    public sealed partial record Chromatic : Interval
    {
        public Semitones Value { get; init; }
    }

    public partial record Diatonic : Interval
    {
        public Quality Quality { get; init; }
    }

    public partial record Simple : Diatonic
    {
        public DiatonicNumber Number { get; init; }

        public Simple ToInverse() => new() {Number = !Number, Quality = !Quality};
        public static Simple operator !(Simple interval) => interval.ToInverse();
    }

    public partial record Compound : Diatonic
    {
        public CompoundDiatonicNumber Number { get; init; }
    }
}