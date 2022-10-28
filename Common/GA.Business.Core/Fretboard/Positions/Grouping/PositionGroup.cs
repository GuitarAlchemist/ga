using GA.Business.Core.Fretboard.Primitives;

namespace GA.Business.Core.Fretboard.Positions.Grouping;

public class PositionGroup : IReadOnlyCollection<Position>
{
    #region Equality Members

    protected bool Equals(PositionGroup other) => _hashSet.Equals(other._hashSet);

    public IEnumerator<Position> GetEnumerator()
    {
        return Positions.GetEnumerator();
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((PositionGroup)obj);
    }

    public override int GetHashCode() => _hashSet.GetHashCode();
    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable)Positions).GetEnumerator();
    }

    public static bool operator ==(PositionGroup? left, PositionGroup? right) => Equals(left, right);
    public static bool operator !=(PositionGroup? left, PositionGroup? right) => !Equals(left, right);

    #endregion

    private readonly ImmutableHashSet<Position> _hashSet;

    public PositionGroup(IReadOnlyCollection<Position> positions)
    {
        Positions = positions;
        _hashSet = positions.ToImmutableHashSet();
    }

    public IReadOnlyCollection<Position> Positions { get; }
    public int Count => Positions.Count;
}