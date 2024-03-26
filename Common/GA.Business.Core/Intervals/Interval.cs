namespace GA.Business.Core.Intervals;

using Primitives;

/// <summary>
/// Interval discriminated union
/// </summary>
/// <remarks>
/// Subclasses: <see cref="Chromatic"/> | <see cref="Diatonic.Simple"/> | <see cref="Diatonic.Compound"/>
/// </remarks>
[PublicAPI]
public abstract record Interval
{
    /// <summary>
    /// Get the number of semitones for the current <see cref="Interval"/>
    /// </summary>
    /// <returns>
    /// The <see cref="Semitones"/>
    /// </returns>
    public abstract Semitones ToSemitones();

    #region Chromatic

    /// <inheritdoc cref="Interval"/>
    /// <summary>
    /// A chromatic interval
    /// </summary>
    /// <remarks>
    /// <see href="https://viva.pressbooks.pub/openmusictheory/chapter/intervals-in-integer-notation/"/> 
    /// <see href="http://musictheoryblog.blogspot.com/2007/01/intervals.html"/> 
    /// </remarks>
    [PublicAPI]
    public sealed record Chromatic : Interval
    {
        /// <summary>
        /// Gets <see cref="Semitones"/> interval size
        /// </summary>
        public Semitones Size { get; init; }

        public static Chromatic Unison => Create(Semitones.Unison);
        public static Chromatic Semitone => Create(Semitones.Semitone);
        public static Chromatic Tone => Create(Semitones.Tone);
        public static Chromatic Tritone => Create(Semitones.Tritone);
        public static Chromatic Octave => Create(Semitones.Octave());
        private static Chromatic Create(Semitones size) => new() { Size = size };

        public static implicit operator Chromatic(int size) => new() { Size = size };
        public static implicit operator int(Chromatic interval) => interval.Size.Value;
        public static implicit operator Semitones(Chromatic interval) => interval.Size;
        public static Chromatic operator !(Chromatic interval) => Create(!interval.Size);

        public override Semitones ToSemitones() => Size;
    }

    #endregion

    #region Diatonic

    /// <summary>
    /// Diatonic interval discriminated union
    /// </summary>
    /// <remarks>
    /// Inherits from <see cref="Interval"/>
    /// Subclasses: <see cref="Interval.Diatonic.Simple"/> | <see cref="Interval.Diatonic.Compound"/>
    /// </remarks>    
    public abstract record Diatonic : Interval
    {
        /// <summary>
        /// Gets <see cref="IntervalQuality"/>
        /// </summary>
        public IntervalQuality Quality { get; init; }
    }

    /// <summary>
    /// Simple diatonic interval (Within one octave)
    /// </summary>
    public sealed record Simple : Diatonic, IFormattable
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

        #region IFormattable Members

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

        #endregion
        
        #region Well-known intervals

#pragma warning disable IDE1006 // Naming Styles
        
        // ReSharper disable InconsistentNaming

        
        /// <summary>
        /// A unison <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static readonly Simple Unison = new() { Size = IntervalSize.Unison, Quality = IntervalQuality.Perfect };
        
        /// <summary>
        /// A minor third <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static readonly Simple MinorThird = new() { Size = IntervalSize.Third, Quality = IntervalQuality.Minor };

        /// <summary>
        /// A perfect unison <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple P1 => new() { Quality = IntervalQuality.Perfect, Size = IntervalSize.Unison };

        /// <summary>
        /// A minor second <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple m2 => new() { Quality = IntervalQuality.Minor, Size = IntervalSize.Second };
        
        /// <summary>
        /// A major second <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple M2 => new() { Quality = IntervalQuality.Major, Size = IntervalSize.Second };
        
        /// <summary>
        /// A minor third <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple m3 => new() { Quality = IntervalQuality.Minor, Size = IntervalSize.Third };
        
        /// <summary>
        /// A major third <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple M3 => new() { Quality = IntervalQuality.Major, Size = IntervalSize.Third };
        
        /// <summary>
        /// A perfect fourth <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple P4 => new() { Quality = IntervalQuality.Perfect, Size = IntervalSize.Fourth };
        
        /// <summary>
        /// A perfect fifth <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple P5 => new() { Quality = IntervalQuality.Perfect, Size = IntervalSize.Fifth };
        
        /// <summary>
        /// A perfect minor sixth <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple m6 => new() { Quality = IntervalQuality.Minor, Size = IntervalSize.Sixth };
        
        /// <summary>
        /// A perfect major sixth <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple M6 => new() { Quality = IntervalQuality.Major, Size = IntervalSize.Sixth };
        
        /// <summary>
        /// A perfect minor seventh <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple m7 => new() { Quality = IntervalQuality.Minor, Size = IntervalSize.Seventh };
        
        /// <summary>
        /// A perfect major seventh <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple M7 => new() { Quality = IntervalQuality.Major, Size = IntervalSize.Seventh };
        
        /// <summary>
        /// A perfect octave <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        public static Simple P8 => new() { Quality = IntervalQuality.Perfect, Size = IntervalSize.Octave };
        
        // ReSharper restore InconsistentNaming
        
#pragma warning restore IDE1006 // Naming Styles
        
        #endregion
       
        /// <summary>
        /// Gets the inverse of the current <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        /// <param name="interval"></param>
        /// <returns></returns>
        public static Simple operator !(Simple interval) => interval.ToInverse();

        /// <summary>
        /// Gets the <see cref="IntervalSize"/>
        /// </summary>
        public IntervalSize Size { get; init; }

        /// <summary>
        /// Gets the interval short name <see cref="string"/> (e.g. "A4")
        /// </summary>
        public string Name => $"{Quality}{Size}";
        
        /// <summary>
        /// Gets the interval long name <see cref="string"/> (e.g. "Augmented Fourth")
        /// </summary>
        public string LongName => $"{Quality}{Size}";
        
        /// <summary>
        /// Gets the accidented interval name <see cref="string"/> (e.g. "7bb")
        /// </summary>
        public string AccidentedName => GetAccidentedName(Size, Quality);

        /// <summary>
        /// Gets the inverse of the current <see cref="Interval.Diatonic.Simple"/>
        /// </summary>
        /// <returns>The <see cref="Interval.Diatonic.Simple"/> inverse interval</returns>
        public Simple ToInverse() => new() { Size = !Size, Quality = !Quality };
        
        /// <inheritdoc />
        public override string ToString() => ToString("Gm");

        /// <inheritdoc />
        public override Semitones ToSemitones()
        {
            var result = Size.ToSemitones();
            var accidental = Quality.ToAccidental(Size.Consonance);
            if (accidental.HasValue) result += accidental.Value;
            return result;
        }

        /// <summary>
        /// Gets the accidented name of the interval (e.g. "7bb")
        /// </summary>
        /// <param name="size">The <see cref="IntervalSize"/></param>
        /// <param name="quality">The <see cref="IntervalQuality"/></param>
        /// <returns>The accidented name <see cref="string"/></returns>
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

    /// <summary>
    /// Compound diatonic interval (Covers two octaves)
    /// </summary>    
    public sealed record Compound : Diatonic, IFormattable
    {
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
        
        /// <summary>
        /// Gets the <see cref="CompoundIntervalSize"/>
        /// </summary>
        public CompoundIntervalSize Size { get; init; }

        /// <summary>
        /// Gets the interval short name <see cref="string"/> (e.g. "M9")
        /// </summary>
        public string ShortName => $"{Quality}{Size}";
        
        /// <inheritdoc cref="Interval.ToSemitones"/>
        public override Semitones ToSemitones() => Size.ToSemitones() + Quality.ToAccidental(Size.Consonance)?.Value ?? 0;

        /// <inheritdoc />
        public override string ToString() => ToString("G");
    }

    #endregion
}