namespace GA.Business.Core.Fretboard.Positions;

public class PositionLocationSet : IReadOnlySet<PositionLocation>
{
    #region Equality Members

    public static bool operator ==(PositionLocationSet? left, PositionLocationSet? right) => Equals(left, right);
    public static bool operator !=(PositionLocationSet? left, PositionLocationSet? right) => !Equals(left, right);

    public override int GetHashCode() => _set.GetHashCode();

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        return obj.GetType() == GetType() && Equals((PositionLocationSet)obj);
    }

    protected bool Equals(PositionLocationSet other) => _set.SetEquals(other);

    #endregion

    #region IReadOnlySet members

    IEnumerator IEnumerable.GetEnumerator() => _set.GetEnumerator();
    public IEnumerator<PositionLocation> GetEnumerator() => _set.GetEnumerator();
    public int Count => _set.Count;
    public bool Contains(PositionLocation item) => _set.Contains(item);
    public bool IsProperSubsetOf(IEnumerable<PositionLocation> other) => _set.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<PositionLocation> other) => _set.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<PositionLocation> other) => _set.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<PositionLocation> other) => _set.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<PositionLocation> other) => _set.Overlaps(other);
    public bool SetEquals(IEnumerable<PositionLocation> other) => _set.SetEquals(other);

    #endregion

    private readonly ImmutableSortedSet<PositionLocation> _set;

    public PositionLocationSet(IEnumerable<PositionLocation> positions) 
    {
        _set = positions.ToImmutableSortedSet(PositionLocation.StrComparer);
    }

    public override string ToString() => string.Join(" ", _set.Select(location => location.Fret));
}
