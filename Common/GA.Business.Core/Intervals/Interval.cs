namespace GA.Business.Core.Intervals;

using Atonal;
using Atonal.Abstractions;
using Primitives;

/// <summary>
///     Interval discriminated union
/// </summary>
/// <remarks>
///     Subclasses: <see cref="Chromatic" /> | <see cref="Diatonic.Simple" /> | <see cref="Diatonic.Compound" />
/// </remarks>
[PublicAPI]
public abstract partial record Interval : IComparable<Interval>, IComparable
{
    /// <summary>
    ///     Get the number of semitones for the current <see cref="Interval" />
    /// </summary>
    /// <returns>
    ///     The <see cref="Semitones" />
    /// </returns>
    public abstract Semitones Semitones { get; }

    #region IComparable<Interval> Members

    public int CompareTo(Interval? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        return other is null
            ? 1
            : Semitones.CompareTo(other.Semitones);
    }

    #endregion

    #region Chromatic

    /// <inheritdoc cref="Interval" />
    /// <summary>
    ///     A chromatic interval
    /// </summary>
    /// <remarks>
    ///     <see href="https://viva.pressbooks.pub/openmusictheory/chapter/intervals-in-integer-notation/" />
    ///     <see href="http://musictheoryblog.blogspot.com/2007/01/intervals.html" />
    /// </remarks>
    [PublicAPI]
    public sealed record Chromatic(Semitones Size) : Interval, IParsable<Chromatic>, IPitchClass
    {
        public static Chromatic Unison => new(Semitones.Unison);
        public static Chromatic Semitone => new(Semitones.Semitone);
        public static Chromatic Tone => new(Semitones.Tone);
        public static Chromatic Tritone => new(Semitones.Tritone);
        public static Chromatic Octave => new(Semitones.Octave());

        /// <inheritdoc />
        public override Semitones Semitones => Size;

        #region IPitchClass Members

        /// <inheritdoc />
        public PitchClass PitchClass => PitchClass.FromSemitones(Size);

        #endregion

        public static implicit operator Chromatic(int size)
        {
            return new Chromatic((Semitones)size);
        }

        public static implicit operator int(Chromatic interval)
        {
            return interval.Size.Value;
        }

        public static implicit operator Semitones(Chromatic interval)
        {
            return interval.Size;
        }

        public static Chromatic operator !(Chromatic interval)
        {
            return new Chromatic(!interval.Size);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Size.ToString();
        }

        #region IParsable<Semitones>

        public static Chromatic Parse(string s, IFormatProvider? provider)
        {
            if (!TryParse(s, provider, out var result))
            {
                throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
            }

            return result;
        }

        public static bool TryParse(string? s, IFormatProvider? provider, out Chromatic result)
        {
            result = default!;

            if (int.TryParse(s, out var value))
            {
                result = new(value);
                return true;
            }

            switch (s)
            {
                case "H":
                case "S":
                    result = new(1);
                    return true;
                case "T":
                case "W":
                    result = new(2);
                    return true;
            }

            return false;
        }

        #endregion
    }

    #endregion

    #region IComparable Members

    public int CompareTo(object? obj)
    {
        if (obj is null)
        {
            return 1;
        }

        if (ReferenceEquals(this, obj))
        {
            return 0;
        }

        return obj is Interval other
            ? CompareTo(other)
            : throw new ArgumentException($"Object must be of type {nameof(Interval)}");
    }

    public static bool operator <(Interval? left, Interval? right)
    {
        return Comparer<Interval>.Default.Compare(left, right) < 0;
    }

    public static bool operator >(Interval? left, Interval? right)
    {
        return Comparer<Interval>.Default.Compare(left, right) > 0;
    }

    public static bool operator <=(Interval? left, Interval? right)
    {
        return Comparer<Interval>.Default.Compare(left, right) <= 0;
    }

    public static bool operator >=(Interval? left, Interval? right)
    {
        return Comparer<Interval>.Default.Compare(left, right) >= 0;
    }

    #endregion

    #region Diatonic

    /// <summary>
    ///     Diatonic interval discriminated union (e.g. M3, P11)
    /// </summary>
    /// <remarks>
    ///     <see cref="Interval.Simple" /> | <see cref="Interval.Compound" /><br />
    ///     Inherits from <see cref="Interval" />
    /// </remarks>
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
        /// <returns></returns>
        public Accidental? IntervalAccidental => Quality.ToAccidental(Size.Consonance);

        public abstract FormulaIntervalBase ToFormulaInterval();

        /// <summary>
        ///     Gets the <see cref="IIntervalSize" />
        /// </summary>
        /// <returns></returns>
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
            var result =
                accidental.HasValue
                    ? $"{accidental.Value}{size}"
                    : $"{size}";

            return result;
        }

        #region Formats

        [PublicAPI]
        public static class Format
        {
            /// <summary>
            ///     Short name format (e.g. "A4")
            /// </summary>
            public const string ShortName = "G";

            /// <summary>
            ///     Accidented
            /// </summary>
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
                // Success
                result = simpleInterval;
                return true;
            }

            if (Compound.TryParse(s, provider, out var compoundInterval))
            {
                // Success
                result = compoundInterval;
                return true;
            }

            // Failure
            result = default!;
            return false;
        }

        #endregion
    }

    /// <summary>
    ///     Simple diatonic interval (Within one octave)
    /// </summary>
    public sealed partial record Simple : Diatonic, IParsable<Simple>, IFormattable
    {
        /// <summary>
        ///     Gets the <see cref="SimpleIntervalSize" />
        /// </summary>
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
                if (accidental.HasValue)
                {
                    result += accidental.Value;
                }

                return result;
            }
        }

        /// <summary>
        ///     Gets the inverse of the current <see cref="Interval.Simple" />
        /// </summary>
        /// <param name="interval"></param>
        /// <returns></returns>
        public static Simple operator !(Simple interval)
        {
            return interval.ToInverse();
        }

        /// <summary>
        ///     Gets the inverse of the current <see cref="Interval.Simple" />
        /// </summary>
        /// <returns>The <see cref="Interval.Simple" /> inverse interval</returns>
        public Simple ToInverse()
        {
            return new Simple { Size = !Size, Quality = !Quality };
        }

        /// <summary>
        ///     Gets the compound of the current <see cref="Interval.Simple" />
        /// </summary>
        /// <returns>The <see cref="Interval.Compound" /></returns>
        public Compound ToCompound()
        {
            return new Compound { Size = Size.ToCompound(), Quality = Quality };
        }

        /// <summary>
        ///     Gets the formula interval
        /// </summary>
        /// <returns>The <see cref="FormulaSimpleInterval" /></returns>
        public override FormulaSimpleInterval ToFormulaInterval()
        {
            return new FormulaSimpleInterval(Size, Quality);
        }

        /// <inheritdoc />
        /// <remarks>
        ///     Uses <see cref="Interval.Diatonic.Format.ShortName" />
        /// </remarks>
        public override string ToString()
        {
            return ToString(Format.ShortName);
        }

        protected override IIntervalSize GetIntervalSize()
        {
            return Size;
        }

        #region IParsable<Simple> Members

        /// <inheritdoc />
        public new static Simple Parse(string s, IFormatProvider? provider)
        {
            if (!TryParse(s, provider, out var result))
            {
                throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
            }

            return result;
        }

        /// <inheritdoc />
        public static bool TryParse(string? s, IFormatProvider? provider, out Simple result)
        {
            result = null!;

            if (string.IsNullOrWhiteSpace(s))
            {
                return false; // Failure (Empty string)
            }

            var regex = SimpleIntervalRegex();
            var match = regex.Match(s);
            if (!match.Success)
            {
                return false; // Failure
            }

            var prefix = match.Groups["prefix"].Value;
            var number = match.Groups["number"].Value;

            if (!SimpleIntervalSize.TryParse(number, null, out var size))
            {
                return false; // Failure
            }

            if (IntervalQuality.TryParse(prefix, null, out var quality))
            {
                // Success
                result = new() { Size = size, Quality = quality };
                return true;
            }

            if (string.IsNullOrEmpty(prefix) || !Accidental.TryParse(prefix, null, out var accidental))
            {
                accidental = Accidental.Natural;
            }

            if (!IntervalQuality.TryGetFromAccidental(size.Consonance, accidental, out quality))
            {
                return false;
            }

            {
                // Success
                result = new() { Size = size, Quality = quality };
                return true;
            }
        }

        [GeneratedRegex("^(?'prefix'.*)?(?'number'[1-8])$")]
        private static partial Regex SimpleIntervalRegex();

        #endregion

        #region IFormattable Members

        /// <inheritdoc cref="IFormattable" />
        public string ToString(string format)
        {
            return ToString(format, null);
        }

        /// <inheritdoc cref="IFormattable" />
        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            return format?.ToUpperInvariant() switch
            {
                "G" => Name,
                "L" => LongName,
                "A" => AccidentedName,
                null => Name,
                _ => throw new FormatException($"The {format} format string is not supported.")
            };
        }

        #endregion

        #region Well-known intervals

#pragma warning disable IDE1006 // Naming Styles

        // ReSharper disable InconsistentNaming

        /// <summary>
        ///     A unison <see cref="Interval.Simple" />
        /// </summary>
        public static readonly Simple Unison = new()
            { Size = SimpleIntervalSize.Unison, Quality = IntervalQuality.Perfect };

        /// <summary>
        ///     A minor third <see cref="Interval.Simple" />
        /// </summary>
        public static readonly Simple MinorThird = new()
            { Size = SimpleIntervalSize.Third, Quality = IntervalQuality.Minor };

        /// <summary>
        ///     A diminished unison <see cref="Interval.Simple" />
        /// </summary>
        public static Simple dd1 => new()
            { Quality = IntervalQuality.DoublyDiminished, Size = SimpleIntervalSize.Unison };

        /// <summary>
        ///     A diminished unison <see cref="Interval.Simple" />
        /// </summary>
        public static Simple d1 => new() { Quality = IntervalQuality.Diminished, Size = SimpleIntervalSize.Unison };

        /// <summary>
        ///     A perfect unison <see cref="Interval.Simple" />
        /// </summary>
        public static Simple P1 => new() { Quality = IntervalQuality.Perfect, Size = SimpleIntervalSize.Unison };

        /// <summary>
        ///     An augmented unison <see cref="Interval.Simple" />
        /// </summary>
        public static Simple A1 => new() { Quality = IntervalQuality.Augmented, Size = SimpleIntervalSize.Unison };

        /// <summary>
        ///     An double augmented unison <see cref="Interval.Simple" />
        /// </summary>
        public static Simple AA1 => new()
            { Quality = IntervalQuality.DoublyAugmented, Size = SimpleIntervalSize.Unison };

        /// <summary>
        ///     A double diminished second <see cref="Interval.Simple" />
        /// </summary>
        public static Simple dd2 => new()
            { Quality = IntervalQuality.DoublyDiminished, Size = SimpleIntervalSize.Second };

        /// <summary>
        ///     A diminished second <see cref="Interval.Simple" />
        /// </summary>
        public static Simple d2 => new() { Quality = IntervalQuality.Diminished, Size = SimpleIntervalSize.Second };

        /// <summary>
        ///     A minor second <see cref="Interval.Simple" />
        /// </summary>
        public static Simple m2 => new() { Quality = IntervalQuality.Minor, Size = SimpleIntervalSize.Second };

        /// <summary>
        ///     A major second <see cref="Interval.Simple" />
        /// </summary>
        public static Simple M2 => new() { Quality = IntervalQuality.Major, Size = SimpleIntervalSize.Second };

        /// <summary>
        ///     An augmented second <see cref="Interval.Simple" />
        /// </summary>
        public static Simple A2 => new() { Quality = IntervalQuality.Augmented, Size = SimpleIntervalSize.Second };

        /// <summary>
        ///     A double augmented second <see cref="Interval.Simple" />
        /// </summary>
        public static Simple AA2 => new()
            { Quality = IntervalQuality.DoublyAugmented, Size = SimpleIntervalSize.Second };

        /// <summary>
        ///     A minor third <see cref="Interval.Simple" />
        /// </summary>
        public static Simple m3 => new() { Quality = IntervalQuality.Minor, Size = SimpleIntervalSize.Third };

        /// <summary>
        ///     A major third <see cref="Interval.Simple" />
        /// </summary>
        public static Simple M3 => new() { Quality = IntervalQuality.Major, Size = SimpleIntervalSize.Third };

        /// <summary>
        ///     A perfect fourth <see cref="Interval.Simple" />
        /// </summary>
        public static Simple P4 => new() { Quality = IntervalQuality.Perfect, Size = SimpleIntervalSize.Fourth };

        /// <summary>
        ///     A diminished fifth <see cref="Interval.Simple" />
        /// </summary>
        public static Simple d5 => new() { Quality = IntervalQuality.Diminished, Size = SimpleIntervalSize.Fifth };

        /// <summary>
        ///     A perfect fifth <see cref="Interval.Simple" />
        /// </summary>
        public static Simple P5 => new() { Quality = IntervalQuality.Perfect, Size = SimpleIntervalSize.Fifth };

        /// <summary>
        ///     A perfect minor sixth <see cref="Interval.Simple" />
        /// </summary>
        public static Simple m6 => new() { Quality = IntervalQuality.Minor, Size = SimpleIntervalSize.Sixth };

        /// <summary>
        ///     A perfect major sixth <see cref="Interval.Simple" />
        /// </summary>
        public static Simple M6 => new() { Quality = IntervalQuality.Major, Size = SimpleIntervalSize.Sixth };

        /// <summary>
        ///     A perfect minor seventh <see cref="Interval.Simple" />
        /// </summary>
        public static Simple m7 => new() { Quality = IntervalQuality.Minor, Size = SimpleIntervalSize.Seventh };

        /// <summary>
        ///     A perfect major seventh <see cref="Interval.Simple" />
        /// </summary>
        public static Simple M7 => new() { Quality = IntervalQuality.Major, Size = SimpleIntervalSize.Seventh };

        /// <summary>
        ///     A perfect octave <see cref="Interval.Simple" />
        /// </summary>
        public static Simple P8 => new() { Quality = IntervalQuality.Perfect, Size = SimpleIntervalSize.Octave };

        // ReSharper restore InconsistentNaming

#pragma warning restore IDE1006 // Naming Styles

        #endregion
    }

    /// <summary>
    ///     Compound diatonic interval (Larger than one octave)
    /// </summary>
    public sealed partial record Compound : Diatonic, IParsable<Compound>, IFormattable
    {
        /// <inheritdoc />
        public override string Name => $"{Quality}{Size}";

        /// <inheritdoc />
        public override string LongName => $"{Quality}{Size}";

        /// <inheritdoc />
        public override string AccidentedName => GetAccidentedName(Size, Quality);

        /// <summary>
        ///     Gets the <see cref="CompoundIntervalSize" />
        /// </summary>
        public new CompoundIntervalSize Size { get; init; }

        /// <summary>
        ///     Gets the interval short name <see cref="string" /> (e.g. "M9")
        /// </summary>
        public string ShortName => $"{Quality}{Size}";

        /// <inheritdoc cref="Interval.Semitones" />
        public override Semitones Semitones
        {
            get
            {
                var result = Size.Semitones;
                var accidental = Quality.ToAccidental(Size.Consonance);
                if (accidental.HasValue)
                {
                    result += accidental.Value;
                }

                return result;
            }
        }

        /// <summary>
        ///     Gets the formula interval
        /// </summary>
        /// <returns>The <see cref="FormulaCompoundInterval" /></returns>
        public override FormulaCompoundInterval ToFormulaInterval()
        {
            return new FormulaCompoundInterval(Size, Quality);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToString("G");
        }

        /// <summary>
        ///     Gets the accidented name of the interval (e.g. "7bb")
        /// </summary>
        /// <param name="size">The <see cref="SimpleIntervalSize" /></param>
        /// <param name="quality">The <see cref="CompoundIntervalSize" /></param>
        /// <returns>The accidented name <see cref="string" /></returns>
        private static string GetAccidentedName(
            CompoundIntervalSize size,
            IntervalQuality quality)
        {
            var accidental = quality.ToAccidental(size.Consonance);
            var result =
                accidental.HasValue
                    ? $"{accidental.Value}{size}"
                    : $"{size}";

            return result;
        }

        protected override IIntervalSize GetIntervalSize()
        {
            return Size;
        }

        #region IParsable<Simple> Members

        /// <inheritdoc />
        public new static Compound Parse(string s, IFormatProvider? provider)
        {
            if (!TryParse(s, provider, out var result))
            {
                throw new ArgumentException($"Failed parsing '{s}'", nameof(s));
            }

            return result;
        }

        /// <inheritdoc />
        public static bool TryParse(string? s, IFormatProvider? provider, out Compound result)
        {
            result = null!;
            if (string.IsNullOrWhiteSpace(s))
            {
                return false; // Failure (Empty string)
            }

            var regex = CompoundIntervalRegex();
            var match = regex.Match(s);
            if (!match.Success)
            {
                return false; // Failure
            }

            var prefix = match.Groups["prefix"].Value;
            var number = match.Groups["number"].Value;

            if (!CompoundIntervalSize.TryParse(number, null, out var size))
            {
                return false; // Failure
            }

            if (IntervalQuality.TryParse(prefix, null, out var quality))
            {
                // Success
                result = new() { Size = size, Quality = quality };
                return true;
            }

            if (string.IsNullOrEmpty(prefix) || !Accidental.TryParse(prefix, null, out var accidental))
            {
                accidental = Accidental.Natural;
            }

            var consonance = size.Consonance;
            if (!IntervalQuality.TryGetFromAccidental(consonance, accidental, out quality))
            {
                return false;
            }

            {
                // Success
                result = new() { Size = size, Quality = quality };
                return true;
            }
        }

        [GeneratedRegex("^(?'prefix'.*)?(?'number'9|10|11|12|13|14|15|16)$")]
        private static partial Regex CompoundIntervalRegex();

        #endregion

        #region IFormattable Members

        public string ToString(string format)
        {
            return ToString(format, null);
        }

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

        /// <summary>
        ///     A minor ninth <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound m9 => new() { Quality = IntervalQuality.Minor, Size = CompoundIntervalSize.Ninth };

        /// <summary>
        ///     A major ninth  <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound M9 => new() { Quality = IntervalQuality.Major, Size = CompoundIntervalSize.Ninth };

        /// <summary>
        ///     A minor tenth <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound m10 => new() { Quality = IntervalQuality.Minor, Size = CompoundIntervalSize.Tenth };

        /// <summary>
        ///     A major tenth <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound M10 => new() { Quality = IntervalQuality.Major, Size = CompoundIntervalSize.Tenth };

        /// <summary>
        ///     A perfect eleventh <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound P11 => new() { Quality = IntervalQuality.Perfect, Size = CompoundIntervalSize.Eleventh };

        /// <summary>
        ///     A perfect eleventh <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound P12 => new() { Quality = IntervalQuality.Perfect, Size = CompoundIntervalSize.Twelfth };

        /// <summary>
        ///     A perfect minor fourteenth <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound m13 => new() { Quality = IntervalQuality.Minor, Size = CompoundIntervalSize.Thirteenth };

        /// <summary>
        ///     A perfect major fourteenth <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound M13 => new() { Quality = IntervalQuality.Major, Size = CompoundIntervalSize.Thirteenth };

        /// <summary>
        ///     A perfect minor fifteenth <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound m14 => new() { Quality = IntervalQuality.Minor, Size = CompoundIntervalSize.Fourteenth };

        /// <summary>
        ///     A perfect major fifteenth <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound M14 => new() { Quality = IntervalQuality.Major, Size = CompoundIntervalSize.Fourteenth };

        /// <summary>
        ///     A perfect octave <see cref="Interval.Diatonic.Compound" />
        /// </summary>
        public static Compound P15 => new()
            { Quality = IntervalQuality.Perfect, Size = CompoundIntervalSize.DoubleOctave };

        // ReSharper restore InconsistentNaming

#pragma warning restore IDE1006 // Naming Styles

        #endregion
    }

    #endregion
}
