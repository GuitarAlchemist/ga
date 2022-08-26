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
        public IntervalQuality Quality { get; init; }
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

        public static readonly Simple Unison = new() {Size = IntervalSize.Unison, Quality = IntervalQuality.Perfect};
        public static readonly Simple MinorThird = new() {Size = IntervalSize.Third, Quality = IntervalQuality.Minor};

        // ReSharper disable InconsistentNaming
        public static Simple P1 => new() {Quality = IntervalQuality.Perfect, Size = IntervalSize.Unison};
        public static Simple m2 => new() {Quality = IntervalQuality.Minor, Size = IntervalSize.Second};
        public static Simple M2 => new() {Quality = IntervalQuality.Major, Size = IntervalSize.Second};
        public static Simple m3 => new() {Quality = IntervalQuality.Minor, Size = IntervalSize.Third};
        public static Simple M3 => new() {Quality = IntervalQuality.Major, Size = IntervalSize.Third};
        public static Simple P4 => new() {Quality = IntervalQuality.Perfect, Size = IntervalSize.Fourth};
        public static Simple P5 => new() {Quality = IntervalQuality.Perfect, Size = IntervalSize.Fifth};
        public static Simple m6 => new() {Quality = IntervalQuality.Minor, Size = IntervalSize.Sixth};
        public static Simple M6 => new() {Quality = IntervalQuality.Major, Size = IntervalSize.Sixth};
        public static Simple m7 => new() {Quality = IntervalQuality.Minor, Size = IntervalSize.Seventh};
        public static Simple M7 => new() {Quality = IntervalQuality.Major, Size = IntervalSize.Seventh};
        public static Simple P8 => new() {Quality = IntervalQuality.Perfect, Size = IntervalSize.Octave};
        // ReSharper restore InconsistentNaming

        public static Simple operator !(Simple interval) => interval.ToInverse();

        public IntervalSize Size { get; init; }

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

        public override Semitones ToSemitones()
        {
            var result = Size.ToSemitones();
            var accidental = Quality.ToAccidental(Size.Consonance);
            if (accidental.HasValue)
            {
                result += accidental.Value;
            }

            return result;
        }
        
        private static string GetAccidentedName(
            IntervalSize size,
            IntervalQuality quality)
        {
            var accidental = quality.ToAccidental(size.Consonance);
            var result =
                accidental.HasValue
                    ? $"{accidental.Value}{size}"
                    : $"{size}";

            return result;
        }
    }

    public sealed partial record Compound : Diatonic, IFormattable
    {
        public CompoundIntervalSize Size { get; init; }

        public string ShortName => $"{Quality}{Size}";

        public override Semitones ToSemitones()
        {
            var result = 
                Size.ToSemitones() + 
                Quality.ToAccidental(Size.Consonance)?.Value ?? 0;

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