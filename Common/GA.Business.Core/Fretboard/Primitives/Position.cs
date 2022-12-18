namespace GA.Business.Core.Fretboard.Primitives;

using GA.Business.Core.Notes.Primitives;
using Positions;

[PublicAPI]
[DiscriminatedUnion(Flatten = true)]
public abstract partial record Position
{
    /// <inheritdoc cref="Position"/>
    public sealed partial record Muted(Str Str) : Position, IComparable<Muted>
    {
        #region Relational Members

        public int CompareTo(Muted? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return Str.CompareTo(other.Str);
        }

        public static bool operator <(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) < 0;
        public static bool operator >(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) > 0;
        public static bool operator <=(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) <= 0;
        public static bool operator >=(Muted? left, Muted? right) => Comparer<Muted>.Default.Compare(left, right) >= 0;

        #endregion

        #region Equality Members

        public bool Equals(Muted? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Str.Equals(other.Str);
        }

        public override int GetHashCode() => Str.Value;

        #endregion

        public override string ToString() => $"X{Str}";
    }

    /// <inheritdoc cref="Position"/>
    public sealed partial record Played(PositionLocation Location, MidiNote MidiNote) : Position, IComparable<Played>
    {
        #region Relational Members

        public int CompareTo(Played? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
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
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Location.Equals(other.Location);
        }

        public override int GetHashCode() => Location.GetHashCode();

        #endregion

        public override string ToString() => $"{Location} {MidiNote}";

    }
}
