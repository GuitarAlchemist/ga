namespace GA.Business.Core.Intervals;

using Primitives;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Interval
{
    public abstract Semitones ToSemitones();

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

        public override Semitones ToSemitones() => Size;
    }

    public abstract record Diatonic : Interval
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

        public static readonly Simple Unison = new() {Number = DiatonicNumber.Unison, Quality = Quality.Perfect};
        public static readonly Simple MinorThird = new() {Number = DiatonicNumber.Third, Quality = Quality.Minor};

        public static Simple operator !(Simple interval) => interval.ToInverse();

        public DiatonicNumber Number { get; init; }

        public string Name => $"{Quality}{Number}";
        public string LongName => $"{Quality}{Number}";
        public string AccidentedName => GetAccidentedName(Number, Quality);

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

        public Simple ToInverse() => new() {Number = !Number, Quality = !Quality};

        public override Semitones ToSemitones()
        {
            var result = Number.ToSemitones();
            var accidental = Quality.ToAccidental(Number.IsPerfect);
            if (accidental.HasValue)
            {
                result += accidental.Value;
            }

            return result;
        }
        
        private static string GetAccidentedName(
            DiatonicNumber number,
            Quality quality)
        {
            var accidental = quality.ToAccidental(number.IsPerfect);
            var result =
                accidental.HasValue
                    ? $"{accidental.Value}{number}"
                    : $"{number}";

            return result;
        }
    }

    public sealed partial record Compound : Diatonic, IFormattable
    {
        public CompoundDiatonicNumber Number { get; init; }

        public string ShortName => $"{Quality}{Number}";

        public override Semitones ToSemitones()
        {
            var result = 
                Number.ToSemitones() + 
                Quality.ToAccidental(Number.IsPerfect)?.Value ?? 0;

            return result;

        }

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