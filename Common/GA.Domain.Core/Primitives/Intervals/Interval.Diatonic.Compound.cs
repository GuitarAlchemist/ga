namespace GA.Domain.Core.Primitives.Intervals;

using System.Text.RegularExpressions;
using Notes;
using Theory.Atonal.Abstractions;

public abstract partial record Interval
{
    /// <summary>
    ///     Compound diatonic interval (larger than one octave)
    /// </summary>
    [PublicAPI]
    public sealed partial record Compound : Diatonic, IParsable<Compound>, IFormattable
    {
        /// <inheritdoc />
        public override string Name => $"{Quality}{Size}";

        /// <inheritdoc />
        public override string LongName => $"{Quality}{Size}";

        /// <inheritdoc />
        public override string AccidentedName => GetAccidentedName(Size, Quality);

        /// <summary>Gets the <see cref="CompoundIntervalSize" /></summary>
        public new CompoundIntervalSize Size { get; init; }

        /// <summary>Gets the interval short name (e.g. "M9")</summary>
        public string ShortName => $"{Quality}{Size}";

        /// <inheritdoc cref="Interval.Semitones" />
        public override Semitones Semitones
        {
            get
            {
                var result = Size.Semitones;
                var accidental = Quality.ToAccidental(Size.Consonance);
                if (accidental.HasValue) result += accidental.Value;
                return result;
            }
        }

        /// <inheritdoc />
        public override string ToString() => ToString("G");

        private static string GetAccidentedName(CompoundIntervalSize size, IntervalQuality quality)
        {
            var accidental = quality.ToAccidental(size.Consonance);
            return accidental.HasValue ? $"{accidental.Value}{size}" : $"{size}";
        }

        protected override IIntervalSize GetIntervalSize() => Size;

        #region IParsable<Compound> Members

        /// <inheritdoc />
        public new static Compound Parse(string s, IFormatProvider? provider)
        {
            if (!TryParse(s, provider, out var result))
                throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
            return result;
        }

        /// <inheritdoc />
        public static bool TryParse(string? s, IFormatProvider? provider, out Compound result)
        {
            result = null!;
            if (string.IsNullOrWhiteSpace(s)) return false;

            var match = CompoundIntervalRegex().Match(s);
            if (!match.Success) return false;

            var prefix = match.Groups["prefix"].Value;
            var number = match.Groups["number"].Value;

            if (!CompoundIntervalSize.TryParse(number, null, out var size)) return false;

            if (IntervalQuality.TryParse(prefix, null, out var quality))
            {
                result = new() { Size = size, Quality = quality };
                return true;
            }

            if (string.IsNullOrEmpty(prefix) || !Accidental.TryParse(prefix, null, out var accidental))
                accidental = Accidental.Natural;

            if (!IntervalQuality.TryGetFromAccidental(size.Consonance, accidental, out quality))
                return false;

            result = new() { Size = size, Quality = quality };
            return true;
        }

        [GeneratedRegex("^(?'prefix'.*)?(?'number'9|10|11|12|13|14|15|16)$")]
        private static partial Regex CompoundIntervalRegex();

        #endregion

        #region IFormattable Members

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

        #endregion

        #region Well-known intervals

#pragma warning disable IDE1006 // Naming Styles
        // ReSharper disable InconsistentNaming

        public static Compound m9  => new() { Quality = IntervalQuality.Minor,   Size = CompoundIntervalSize.Ninth };
        public static Compound M9  => new() { Quality = IntervalQuality.Major,   Size = CompoundIntervalSize.Ninth };
        public static Compound m10 => new() { Quality = IntervalQuality.Minor,   Size = CompoundIntervalSize.Tenth };
        public static Compound M10 => new() { Quality = IntervalQuality.Major,   Size = CompoundIntervalSize.Tenth };
        public static Compound P11 => new() { Quality = IntervalQuality.Perfect, Size = CompoundIntervalSize.Eleventh };
        public static Compound P12 => new() { Quality = IntervalQuality.Perfect, Size = CompoundIntervalSize.Twelfth };
        public static Compound m13 => new() { Quality = IntervalQuality.Minor,   Size = CompoundIntervalSize.Thirteenth };
        public static Compound M13 => new() { Quality = IntervalQuality.Major,   Size = CompoundIntervalSize.Thirteenth };
        public static Compound m14 => new() { Quality = IntervalQuality.Minor,   Size = CompoundIntervalSize.Fourteenth };
        public static Compound M14 => new() { Quality = IntervalQuality.Major,   Size = CompoundIntervalSize.Fourteenth };
        public static Compound P15 => new() { Quality = IntervalQuality.Perfect, Size = CompoundIntervalSize.DoubleOctave };

        // ReSharper restore InconsistentNaming
#pragma warning restore IDE1006 // Naming Styles

        #endregion
    }
}
