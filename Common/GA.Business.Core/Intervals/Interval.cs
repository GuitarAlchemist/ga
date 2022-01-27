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
    /// http://musictheoryblog.blogspot.com/2007/01/intervals.html
    /// </remarks>
    [PublicAPI]
    public sealed partial record Chromatic : Interval
    {
        public Semitones Size { get; init; }

        public static Chromatic Unison => Create(Semitones.Unison);
        public static Chromatic Semitone => Create(Semitones.Semitone);
        public static Chromatic Tone => Create(Semitones.Tone);
        public static Chromatic Tritone => Create(Semitones.Tritone);
        public static Chromatic Octave => Create(Semitones.Octave());
        private static Chromatic Create(Semitones size) => new() {Size = size};

        public static implicit operator Chromatic(int size) => new() { Size = size};
        public static implicit operator int(Chromatic interval) => interval.Size.Value;
        public static implicit operator Semitones(Chromatic interval) => interval.Size;
        public static Chromatic operator !(Chromatic interval) => Create(!interval.Size);
    }

    public partial record Diatonic : Interval
    {
        public Quality Quality { get; init; }
    }

    public sealed partial record Simple : Diatonic, IFormattable
    {
        #region Formats

        [PublicAPI]
        public static class Format
        {
            /// <summary>
            /// Short name format (e.g. "A4")
            /// </summary>
            public const string ShortName = "G";

            /// <summary>
            /// Accidented 
            /// </summary>
            public const string AccidentedName = "A";
        }

        #endregion

        public DiatonicNumber Size { get; init; }

        public string Name => $"{Quality}{Size}";
        public string LongName => $"{Quality}{Size}";
        public string AccidentedName => GetAccidentedName(Size, Quality);

        public override string ToString() => ToString("G");
        public string ToString(string format) => ToString(format, null);
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            format ??= "G";
            return format.ToUpperInvariant() switch
            {
                "G" => Name,
                "L" => LongName,
                "A" => AccidentedName,
                _ => throw new FormatException($"The {format} format string is not supported.")
            };
        }

        public Simple ToInverse() => new() {Size = !Size, Quality = !Quality};
        public static Simple operator !(Simple interval) => interval.ToInverse();

        private static string GetAccidentedName(
            DiatonicNumber size,
            Quality quality)

        {
            var isPerfectInterval = size.IsPerfect;
            var accidental = quality.ToAccidental(isPerfectInterval);
            var result =
                accidental.HasValue 
                    ? $"{size}"
                    : $"{accidental}{size}";

            return result;
        }
    }

    public sealed partial record Compound : Diatonic, IFormattable
    {
        public CompoundDiatonicNumber Size { get; init; }

        public string ShortName => $"{Quality}{Size}";
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