namespace GA.Domain.Core.Primitives.Intervals;

using Theory.Atonal;
using Theory.Atonal.Abstractions;

public abstract partial record Interval
{
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

        public static implicit operator Chromatic(int size) => new((Semitones)size);

        public static implicit operator int(Chromatic interval) => interval.Size.Value;

        public static implicit operator Semitones(Chromatic interval) => interval.Size;

        public static Chromatic operator !(Chromatic interval) => new(!interval.Size);

        /// <inheritdoc />
        public override string ToString() => Size.ToString();

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
}
