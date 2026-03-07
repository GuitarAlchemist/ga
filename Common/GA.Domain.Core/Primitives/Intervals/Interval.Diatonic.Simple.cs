namespace GA.Domain.Core.Primitives.Intervals;

using System.Text.RegularExpressions;
using Notes;
using Theory.Atonal.Abstractions;

public abstract partial record Interval
{
    /// <summary>
    ///     Simple diatonic interval (within one octave)
    /// </summary>
    [PublicAPI]
    public sealed partial record Simple : Diatonic, IParsable<Simple>, IFormattable
    {
        /// <summary>Gets the <see cref="SimpleIntervalSize" /></summary>
        public new SimpleIntervalSize Size { get; init; }

        /// <inheritdoc />
        public override string Name => $"{Quality}{Size}";

        /// <inheritdoc />
        public override string LongName => $"{Quality}{Size}";

        /// <inheritdoc />
        public override string AccidentedName => GetAccidentedName(Size, Quality);

        /// <inheritdoc />
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

        /// <summary>Gets the inverse of the current <see cref="Interval.Simple" /></summary>
        public static Simple operator !(Simple interval) => interval.ToInverse();

        /// <summary>Gets the inverse of the current <see cref="Interval.Simple" /></summary>
        public Simple ToInverse() => new() { Size = !Size, Quality = !Quality };

        /// <summary>Gets the compound of the current <see cref="Interval.Simple" /></summary>
        public Compound ToCompound() => new() { Size = Size.ToCompound(), Quality = Quality };

        /// <inheritdoc />
        public override string ToString() => ToString(Format.ShortName);

        protected override IIntervalSize GetIntervalSize() => Size;

        #region IParsable<Simple> Members

        /// <inheritdoc />
        public new static Simple Parse(string s, IFormatProvider? provider)
        {
            if (!TryParse(s, provider, out var result))
                throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
            return result;
        }

        /// <inheritdoc />
        public static bool TryParse(string? s, IFormatProvider? provider, out Simple result)
        {
            result = null!;
            if (string.IsNullOrWhiteSpace(s)) return false;

            var match = SimpleIntervalRegex().Match(s);
            if (!match.Success) return false;

            var prefix = match.Groups["prefix"].Value;
            var number = match.Groups["number"].Value;

            if (!SimpleIntervalSize.TryParse(number, null, out var size)) return false;

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

        [GeneratedRegex("^(?'prefix'.*)?(?'number'[1-8])$")]
        private static partial Regex SimpleIntervalRegex();

        #endregion

        #region IFormattable Members

        /// <inheritdoc cref="IFormattable" />
        public string ToString(string format) => ToString(format, null);

        /// <inheritdoc cref="IFormattable" />
        public string ToString(string? format, IFormatProvider? formatProvider) => format?.ToUpperInvariant() switch
        {
            "G" => Name,
            "L" => LongName,
            "A" => AccidentedName,
            null => Name,
            _ => throw new FormatException($"The {format} format string is not supported.")
        };

        #endregion

        #region Well-known intervals

#pragma warning disable IDE1006 // Naming Styles
        // ReSharper disable InconsistentNaming

        public static readonly Simple Unison = new() { Size = SimpleIntervalSize.Unison, Quality = IntervalQuality.Perfect };
        public static readonly Simple MinorThird = new() { Size = SimpleIntervalSize.Third, Quality = IntervalQuality.Minor };

        public static Simple dd1 => new() { Quality = IntervalQuality.DoublyDiminished, Size = SimpleIntervalSize.Unison };
        public static Simple d1  => new() { Quality = IntervalQuality.Diminished,       Size = SimpleIntervalSize.Unison };
        public static Simple P1  => new() { Quality = IntervalQuality.Perfect,           Size = SimpleIntervalSize.Unison };
        public static Simple A1  => new() { Quality = IntervalQuality.Augmented,         Size = SimpleIntervalSize.Unison };
        public static Simple AA1 => new() { Quality = IntervalQuality.DoublyAugmented,   Size = SimpleIntervalSize.Unison };

        public static Simple dd2 => new() { Quality = IntervalQuality.DoublyDiminished, Size = SimpleIntervalSize.Second };
        public static Simple d2  => new() { Quality = IntervalQuality.Diminished,       Size = SimpleIntervalSize.Second };
        public static Simple m2  => new() { Quality = IntervalQuality.Minor,            Size = SimpleIntervalSize.Second };
        public static Simple M2  => new() { Quality = IntervalQuality.Major,            Size = SimpleIntervalSize.Second };
        public static Simple A2  => new() { Quality = IntervalQuality.Augmented,        Size = SimpleIntervalSize.Second };
        public static Simple AA2 => new() { Quality = IntervalQuality.DoublyAugmented,  Size = SimpleIntervalSize.Second };

        public static Simple m3 => new() { Quality = IntervalQuality.Minor,   Size = SimpleIntervalSize.Third };
        public static Simple M3 => new() { Quality = IntervalQuality.Major,   Size = SimpleIntervalSize.Third };
        public static Simple P4 => new() { Quality = IntervalQuality.Perfect, Size = SimpleIntervalSize.Fourth };
        public static Simple d5 => new() { Quality = IntervalQuality.Diminished, Size = SimpleIntervalSize.Fifth };
        public static Simple P5 => new() { Quality = IntervalQuality.Perfect,    Size = SimpleIntervalSize.Fifth };
        public static Simple m6 => new() { Quality = IntervalQuality.Minor,  Size = SimpleIntervalSize.Sixth };
        public static Simple M6 => new() { Quality = IntervalQuality.Major,  Size = SimpleIntervalSize.Sixth };
        public static Simple m7 => new() { Quality = IntervalQuality.Minor,  Size = SimpleIntervalSize.Seventh };
        public static Simple M7 => new() { Quality = IntervalQuality.Major,  Size = SimpleIntervalSize.Seventh };
        public static Simple P8 => new() { Quality = IntervalQuality.Perfect, Size = SimpleIntervalSize.Octave };

        // ReSharper restore InconsistentNaming
#pragma warning restore IDE1006 // Naming Styles

        #endregion
    }
}
