namespace GA.Domain.Core.Primitives.Intervals;

using Notes;
using Theory.Atonal.Abstractions;

public abstract partial record Interval
{
    /// <summary>
    ///     Diatonic interval discriminated union (e.g. M3, P11) (<see href="https://en.wikipedia.org/wiki/Diatonic_scale" />).
    /// </summary>
    /// <remarks>
    ///     <see cref="Interval.Simple" /> | <see cref="Interval.Compound" /><br />
    ///     Inherits from <see cref="Interval" />
    /// </remarks>
    [PublicAPI]
    public abstract record Diatonic : Interval, IParsable<Diatonic>
    {
        /// <summary>
        ///     Gets the <see cref="IIntervalSize" />
        /// </summary>
        public IIntervalSize Size => GetIntervalSize();

        /// <summary>
        ///     Gets <see cref="IntervalQuality" />
        /// </summary>
        public IntervalQuality Quality { get; init; }

        /// <summary>
        ///     Gets the interval short name <see cref="string" /> (e.g. "A4")
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        ///     Gets the interval long name <see cref="string" /> (e.g. "Augmented Fourth")
        /// </summary>
        public abstract string LongName { get; }

        /// <summary>
        ///     Gets the accidented interval name <see cref="string" /> (e.g. "7bb")
        /// </summary>
        public abstract string AccidentedName { get; }

        /// <summary>
        ///     Gets the <see cref="Nullable{Accidental}" />
        /// </summary>
        public Accidental? IntervalAccidental => Quality.ToAccidental(Size.Consonance);

        /// <summary>
        ///     Gets the <see cref="IIntervalSize" />
        /// </summary>
        protected abstract IIntervalSize GetIntervalSize();

        /// <summary>
        ///     Gets the accidented name of the interval (e.g. "7bb")
        /// </summary>
        /// <param name="size">The <see cref="IIntervalSize" /></param>
        /// <param name="quality">The <see cref="IntervalQuality" /></param>
        /// <returns>The accidented name <see cref="string" /></returns>
        protected static string GetAccidentedName(
            IIntervalSize size,
            IntervalQuality quality)
        {
            var accidental = quality.ToAccidental(size.Consonance);
            return accidental.HasValue ? $"{accidental.Value}{size}" : $"{size}";
        }

        #region Formats

        [PublicAPI]
        public static class Format
        {
            /// <summary>Short name format (e.g. "A4")</summary>
            public const string ShortName = "G";

            /// <summary>Accidented format (e.g. "7bb")</summary>
            public const string AccidentedName = "A";
        }

        #endregion

        #region IParsable<Diatonic>

        public static Diatonic Parse(string s, IFormatProvider? provider)
        {
            if (!TryParse(s, provider, out var result))
            {
                throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
            }

            return result;
        }

        public static bool TryParse(string? s, IFormatProvider? provider, out Diatonic result)
        {
            if (Simple.TryParse(s, provider, out var simpleInterval))
            {
                result = simpleInterval;
                return true;
            }

            if (Compound.TryParse(s, provider, out var compoundInterval))
            {
                result = compoundInterval;
                return true;
            }

            result = default!;
            return false;
        }

        #endregion
    }
}
