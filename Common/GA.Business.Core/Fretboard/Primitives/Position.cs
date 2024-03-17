namespace GA.Business.Core.Fretboard.Primitives;

using GA.Business.Core.Notes.Primitives;
using Positions;

[PublicAPI]
public abstract record Position
{
    /// <inheritdoc cref="Position"/>
    public sealed record Muted(Str Str) : Position, IComparable<Muted>
    {
        #region Relational Members

        public int CompareTo(Muted? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            return other is null ? 1 : Str.CompareTo(other.Str);
        }

        public static bool operator <(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) < 0;
        public static bool operator >(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) > 0;
        public static bool operator <=(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) <= 0;
        public static bool operator >=(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) >= 0;

        #endregion

        public override string ToString() => $"X{Str}";
    }

    /// <inheritdoc cref="Position"/>
    public sealed record Played(PositionLocation Location, MidiNote MidiNote) : Position, IComparable<Played>
    {
        #region Relational Members

        public int CompareTo(Played? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (other is null) return 1;
            return Location.CompareTo(other.Location);
        }

        public static bool operator <(Played? left, Played? right) => Comparer<Played>.Default.Compare(left, right) < 0;
        public static bool operator >(Played? left, Played? right) => Comparer<Played>.Default.Compare(left, right) > 0;
        public static bool operator <=(Played? left, Played? right) => Comparer<Played>.Default.Compare(left, right) <= 0;
        public static bool operator >=(Played? left, Played? right) => Comparer<Played>.Default.Compare(left, right) >= 0;

        #endregion

        #region Equality Members

        public bool Equals(Played? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Location.Equals(other.Location);
        }

        public override int GetHashCode() => Location.GetHashCode();

        #endregion

        public override string ToString() => $"{Location} {MidiNote}";

    }
}
