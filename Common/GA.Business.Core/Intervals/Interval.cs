namespace GA.Business.Core.Intervals;

using Primitives;

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
    [PublicAPI]
    public sealed partial record Chromatic : Interval
    {
        public Semitones Quantity { get; init; }

        public static Chromatic Unison => Create(Semitones.Unison);
        public static Chromatic Semitone => Create(Semitones.Semitone);
        public static Chromatic Tone => Create(Semitones.Tone);
        public static Chromatic Tritone => Create(Semitones.Tritone);
        public static Chromatic Octave => Create(Semitones.Octave());
        private static Chromatic Create(Semitones quantity) => new() {Quantity = quantity};

        public static implicit operator Chromatic(int quantity) => new() { Quantity = quantity};
        public static implicit operator int(Chromatic interval) => interval.Quantity.Value;
        public static implicit operator Semitones(Chromatic interval) => interval.Quantity;
        public static Chromatic operator !(Chromatic interval) => Create(!interval.Quantity);

        
        /*
        public void ToSimple()
        {
            foreach (var number in DiatonicNumber.Range(7))
            {
            }
        }
         */
    }

    /// <summary>
    /// 
    /// </summary>
    public partial record Diatonic : Interval
    {
        public Quality Quality { get; init; }
    }

    public sealed partial record Simple : Diatonic, IFormattable
    {
        public DiatonicNumber Quantity { get; init; }

        public string ShortName => $"{Quality}{Quantity}";
        public override string ToString() => ToString("G");
        public string ToString(string format) => ToString(format, null);
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            format ??= "G";
            return format.ToUpperInvariant() switch
            {
                "G" => ShortName,
                "S" => ShortName,
                _ => throw new FormatException($"The {format} format string is not supported.")
            };
        }

        public Simple ToInverse() => new() {Quantity = !Quantity, Quality = !Quality};
        public static Simple operator !(Simple interval) => interval.ToInverse();
    }

    public sealed partial record Compound : Diatonic, IFormattable
    {
        public CompoundDiatonicNumber Quantity { get; init; }

        public string ShortName => $"{Quality}{Quantity}";
        public override string ToString() => ToString("G");
        public string ToString(string format) => ToString(format, null);
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            format ??= "G";
            return format.ToUpperInvariant() switch
            {
                "G" => ShortName,
                "S" => ShortName,
                _ => throw new FormatException($"The {format} format string is not supported.")
            };
        }
    }
}