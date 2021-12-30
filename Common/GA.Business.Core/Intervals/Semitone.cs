using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GA.Business.Core.Intervals;

    /*
    /// <inheritdoc cref="IEquatable{Semitone}" />
    /// <inheritdoc cref="IComparable{Semitone}" />
    /// <summary>
    /// ChromaticInterval as semitones (See http://en.wikipedia.org/wiki/Semitone)
    /// </summary>
    public class Semitone : IEquatable<Semitone>, IComparable<Semitone>
    {
        public static IEqualityComparer<Semitone> DistanceComparer = DistanceSemitoneComparer.Instance;
        public static IEqualityComparer<Semitone> SimpleDistanceComparer = SimpleDistanceSemitoneComparer.Instance;
        public static Semitone Unison = new Semitone(0);
        public static Semitone Octave = new Semitone(12);

        public Semitone(int distance)
        {
            Distance = distance;
        }

        /// <summary>
        /// Gets the <see cref="Int32"/> sign.
        /// </summary>
        public int Sign => Math.Sign(Distance);

        /// <summary>
        /// Gets the distance in semitones (Signed).
        /// </summary>
        public virtual int Distance { get; }

        /// <summary>
        /// Gets the distance in semitones (Absolute)
        /// </summary>
        public virtual int AbsoluteDistance => Math.Abs(Distance);

        public bool IsUnison => Distance == 0;

        public bool IsOctave => Distance == 12;

        /// <summary>
        /// Gets the distance in semitones limited to 1 octave (Absolute).
        /// </summary>
        public int SingleOctaveDistance => AbsoluteDistance % 12;

        /// <summary>
        /// Gets the distance in semitones limited to 2 octave (Absolute).
        /// </summary>
        public int DoubleOctaveDistance => AbsoluteDistance % 24;

        /// <summary>
        /// True if below one octave.
        /// </summary>
        public bool IsSimple => AbsoluteDistance > 0 && AbsoluteDistance < 12;

        /// <summary>
        /// True if over one octave.
        /// </summary>
        public bool IsCompound => AbsoluteDistance >= 12 && AbsoluteDistance < 24;

        /// <summary>
        /// Gets the octave from the current semitone.
        /// </summary>
        /// <returns>The <see cref="Octave"/>.</returns>
        public Octave GetOctave()
        {
            checked
            {
                var octave = (sbyte)(Distance / 12);

                return new Octave(octave);
            }
        }

        public bool Equals(Semitone other)
        {
            return Distance == other.Distance;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Semitone semitone && Equals(semitone);
        }

        public override int GetHashCode()
        {
            return Distance;
        }

        public int CompareTo(Semitone other)
        {
            return Comparer<int>.Default.Compare(Distance, other.Distance);
        }

        /// <summary>
        /// Converts the string representation of a semitone to its semitone equivalent.
        /// </summary>
        /// <param name="sDistance">The <see cref="string"/> represention on the semitone distance (int)</param>
        /// <returns>The <see cref="Semitone"/>.</returns>
        /// <exception cref="FormatException">Throw if the format is incorrect,</exception>
        public static Semitone Parse(string sDistance)
        {
            if (TryParse(sDistance, out var result)) return result;

            throw new FormatException($"'{nameof(sDistance)}' is not the correct format (int)");
        }

        /// <summary>
        /// Converts the string representation of a semitone to its semitone equivalent. A return value indicates whether the conversion succeeded.
        /// </summary>
        /// <param name="sDistance">The <see cref="string"/> represention on the semitone distance (int)</param>
        /// <param name="result">The <see cref="Semitone"/> (null if conversion failed).</param>
        /// <returns>True if the conversion succeeded, false otherwise.</returns>
        public static bool TryParse(string sDistance, out Semitone result)
        {
            result = null;
            if (!int.TryParse(sDistance, out var distance)) return false;

            result = new Semitone(distance);
            return true;
        }

        #region Operators

        public static implicit operator Semitone(string sDistance)
        {
            return Parse(sDistance);
        }

        public static implicit operator Semitone(sbyte value)
        {
            return new Semitone(value);
        }

        public static implicit operator int(Semitone semitone)
        {
            return semitone.Distance;
        }

        /// <summary>
        /// Negates a <see cref="Semitone" /> object.
        /// </summary>
        /// <param name="semitone">The <see cref="Semitone" /></param>
        /// <returns>The inverted <see cref="Semitone" />.</returns>
        public static Semitone operator -(Semitone semitone)
        {
            return new Semitone(-semitone.Distance);
        }

        /// <summary>
        /// "Is Greater Than" comparison between two <see cref="Semitone" /> objects.
        /// </summary>
        /// <param name="a">The first <see cref="Semitone" /></param>
        /// <param name="b">The second <see cref="Semitone" /></param>
        /// <returns>True if the <see cref="Semitone" /> a is greater than to the <see cref="Semitone" /> b</returns>
        public static bool operator >(Semitone a, Semitone b)
        {
            return a.Distance > b.Distance;
        }

        /// <summary>
        /// "Is Greater Or Equal" comparison between two <see cref="Semitone" /> objects.
        /// </summary>
        /// <param name="a">The first <see cref="Semitone" /></param>
        /// <param name="b">The second <see cref="Semitone" /></param>
        /// <returns>True if the <see cref="Semitone" /> a is greater than or equal to the <see cref="Semitone" /> b</returns>
        public static bool operator >=(Semitone a, Semitone b)
        {
            return a.Distance >= b.Distance;
        }

        /// <summary>
        /// "Is Less Than" comparison between two <see cref="Semitone" /> objects.
        /// </summary>
        /// <param name="a">The first <see cref="Semitone" /></param>
        /// <param name="b">The second <see cref="Semitone" /></param>
        /// <returns>True if the <see cref="Semitone" /> a is less than the <see cref="Semitone" /> b</returns>
        public static bool operator <(Semitone a, Semitone b)
        {
            return a.Distance < b.Distance;
        }

        /// <summary>
        /// "Is Less Or Equal" comparison between two <see cref="Semitone" /> objects.
        /// </summary>
        /// <param name="a">The first <see cref="Semitone" /></param>
        /// <param name="b">The second <see cref="Semitone" /></param>
        /// <returns>True if the <see cref="Semitone" /> a is less than or equal to the <see cref="Semitone" /> b</returns>
        public static bool operator <=(Semitone a, Semitone b)
        {
            return a.Distance <= b.Distance;
        }

        /// <summary>
        /// Equality test between two <see cref="Semitone" /> objects.
        /// </summary>
        /// <param name="a">The first <see cref="Semitone" /></param>
        /// <param name="b">The second <see cref="Semitone" /></param>
        /// <returns>True if the <see cref="Semitone" /> a is equal to the <see cref="Semitone" /> b</returns>
        public static bool operator ==(Semitone a, Semitone b)
        {
            if (ReferenceEquals(a, b)) return true;
            var aDistance = a?.Distance ?? 0;
            var bDistance = b?.Distance ?? 0;

            return aDistance == bDistance;
        }

        /// <summary>
        /// Difference test between two <see cref="Semitone" /> objects.
        /// </summary>
        /// <param name="a">The first <see cref="Semitone" /></param>
        /// <param name="b">The second <see cref="Semitone" /></param>
        /// <returns>True if the <see cref="Semitone" /> a is different than the <see cref="Semitone" /> b</returns>
        public static bool operator !=(Semitone a, Semitone b)
        {
            return !(a == b);
        }

        /// <summary>
        /// Adds two <see cref="Semitone" /> objects
        /// </summary>
        /// <param name="a">The first <see cref="Semitone" /></param>
        /// <param name="b">The second <see cref="Semitone" /></param>
        /// <returns>The sum of the two <see cref="Semitone" /> objects</returns>
        public static Semitone operator +(Semitone a, Semitone b)
        {
            return new Semitone(a.Distance + b.Distance);
        }

        /// <summary>
        /// Subtract an <see cref="Semitone" /> to another one
        /// </summary>
        /// <param name="a">The first <see cref="Semitone" /></param>
        /// <param name="b">The second <see cref="Semitone" /></param>
        /// <returns>The <see cref="Semitone" /> subtraction result</returns>
        public static Semitone operator -(Semitone a, Semitone b)
        {
            return new Semitone(a.Distance - b.Distance);
        }

        /// <summary>
        /// Increments a <see cref="Semitone" /> by one.
        /// </summary>
        /// <param name="semitone">The <see cref="Semitone" /></param>
        /// <returns>The resulting <see cref="Semitone" />.</returns>
        public static Semitone operator ++(Semitone semitone)
        {
            return new Semitone(semitone.Distance + 1);
        }

        /// <summary>
        /// Decrements a <see cref="Semitone" /> by one.
        /// </summary>
        /// <param name="semitone">The <see cref="Semitone" /></param>
        /// <returns>The resulting <see cref="Semitone" />.</returns>
        public static Semitone operator --(Semitone semitone)
        {
            return new Semitone(semitone.Distance - 1);
        }

        /// <summary>
        /// Adds octaves to a <see cref="Semitone" />.
        /// </summary>
        /// <param name="semitone">The <see cref="Semitone" /></param>
        /// <param name="octave">The <see cref="Octave" /></param>
        /// <returns>The sum of the two <see cref="Semitone" /> objects</returns>
        public static Semitone operator +(Semitone semitone, Octave octave)
        {
            return new Semitone(semitone.Distance + octave.Distance);
        }

        /// <summary>
        /// Subtracts octaves to a <see cref="Semitone" />.
        /// </summary>
        /// <param name="semitone">The <see cref="Semitone" /> interval.</param>
        /// <param name="octave">The <see cref="Octave" /> interval.</param>
        /// <returns>The sum of the two <see cref="Semitone" /> objects</returns>
        public static Semitone operator -(Semitone semitone, Octave octave)
        {
            return new Semitone(semitone.Distance + octave.Distance);
        }


        /// <summary>
        /// Changes the accidentalKind of a <see cref="Semitone" />.
        /// </summary>
        /// <param name="a">The <see cref="Semitone" /></param>
        /// <param name="accidentalKind">The <see cref="AccidentalKind" /></param>
        /// <returns>The sum of the two <see cref="Semitone" /> objects</returns>
        public static Semitone operator *(Semitone a, AccidentalKind accidentalKind)
        {
            return new Semitone(a.Distance * (int)accidentalKind);
        }

        /// <summary>
        /// Subtracts octaves from a <see cref="Semitone" />.
        /// </summary>
        /// <param name="a">The <see cref="Semitone" /></param>
        /// <param name="octave">The octave <see cref="Semitone" /></param>
        /// <returns>The sum of the two <see cref="Semitone" /> objects</returns>
        public static Semitone operator /(Semitone a, Octave octave)
        {
            return new Semitone(a.Distance - octave.Distance);
        }

        #endregion

        public Semitone ToSimple()
        {
            if (IsSimple) return this;

            var result = new Semitone(AbsoluteDistance % 12);

            return result;
        }

        public Semitone ToCompound()
        {
            if (IsCompound) return this;

            var result = new Semitone(12 + AbsoluteDistance % 12);

            return result;
        }

        public override string ToString()
        {
            return $"{Distance}";
        }

        #region Nested Types

        private class DistanceSemitoneComparer : IEqualityComparer<Semitone>
        {
            public static readonly IEqualityComparer<Semitone> Instance = new DistanceSemitoneComparer();

            public bool Equals(Semitone x, Semitone y)
            {
                var result = x.DoubleOctaveDistance == y.DoubleOctaveDistance;

                return result;
            }

            public int GetHashCode(Semitone obj)
            {
                return obj.DoubleOctaveDistance;
            }

            public override string ToString()
            {
                return "Distance comparer";
            }
        }

        private class SimpleDistanceSemitoneComparer : IEqualityComparer<Semitone>
        {
            public static readonly IEqualityComparer<Semitone> Instance = new SimpleDistanceSemitoneComparer();

            public bool Equals(Semitone x, Semitone y)
            {
                var result = x.SingleOctaveDistance == y.SingleOctaveDistance;

                return result;
            }

            public int GetHashCode(Semitone obj)
            {
                return obj.SingleOctaveDistance;
            }

            public override string ToString()
            {
                return "Simple distance comparer";
            }
        }

        #endregion
    }

    */

