namespace GA.Domain.Core.Instruments.Primitives;

using Core.Primitives.Notes;
using Positions;

/// <summary>
///     Represents a fret position on a guitar string — either a fretted note or a muted string
///     (<see href="https://en.wikipedia.org/wiki/Fret" />).
/// </summary>
[PublicAPI]
public abstract record Position(PositionLocation Location)
{
    /// <inheritdoc cref="Position" />
    public sealed record Muted(Str Str) : Position(new PositionLocation(Str, Fret.Muted)), IComparable<Muted>
    {
        public override string ToString() => $"X{Str}";

        #region Relational Members

        public int CompareTo(Muted? other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            return other is null ? 1 : Str.CompareTo(other.Str);
        }

        public static bool operator <(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) < 0;

        public static bool operator >(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) > 0;

        public static bool operator <=(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) <= 0;

        public static bool operator >=(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) >= 0;

        #endregion
    }

    /// <inheritdoc cref="Position" />
    public sealed record Played(PositionLocation Location, MidiNote MidiNote) : Position(Location), IComparable<Played>
    {
        public override string ToString() => $"{Location} {MidiNote}";

        #region Relational Members

        public int CompareTo(Played? other)
        {
            if (ReferenceEquals(this, other))
            {
                return 0;
            }

            if (other is null)
            {
                return 1;
            }

            return Location.CompareTo(other.Location);
        }

        public static bool operator <(Played? left, Played? right) => Comparer<Played>.Default.Compare(left, right) < 0;

        public static bool operator >(Played? left, Played? right) => Comparer<Played>.Default.Compare(left, right) > 0;

        public static bool operator <=(Played? left, Played? right) =>
            Comparer<Played>.Default.Compare(left, right) <= 0;

        public static bool operator >=(Played? left, Played? right) =>
            Comparer<Played>.Default.Compare(left, right) >= 0;

        #endregion

        #region Equality Members

        public bool Equals(Played? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return base.Equals(other) && Location.Equals(other.Location);
        }

        public override int GetHashCode() => Location.GetHashCode();

        #endregion
    }
}
