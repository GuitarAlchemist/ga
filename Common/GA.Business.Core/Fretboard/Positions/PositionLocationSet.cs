namespace GA.Business.Core.Fretboard.Positions;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

public class PositionLocationSet(IEnumerable<PositionLocation> positions) : IReadOnlySet<PositionLocation>
{
    private readonly ImmutableSortedSet<PositionLocation> _set =
        positions.ToImmutableSortedSet(PositionLocation.StrComparer);

    public override string ToString()
    {
        return string.Join(" ", _set.Select(location => location.Fret));
    }

    #region Equality Members

    public static bool operator ==(PositionLocationSet? left, PositionLocationSet? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(PositionLocationSet? left, PositionLocationSet? right)
    {
        return !Equals(left, right);
    }

    public override int GetHashCode()
    {
        return _set.GetHashCode();
    }

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        return obj.GetType() == GetType() && Equals((PositionLocationSet)obj);
    }

    protected bool Equals(PositionLocationSet other)
    {
        return _set.SetEquals(other);
    }

    #endregion

    #region IReadOnlySet members

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _set.GetEnumerator();
    }

    public IEnumerator<PositionLocation> GetEnumerator()
    {
        return _set.GetEnumerator();
    }

    public int Count => _set.Count;

    public bool Contains(PositionLocation item)
    {
        return _set.Contains(item);
    }

    public bool IsProperSubsetOf(IEnumerable<PositionLocation> other)
    {
        return _set.IsProperSubsetOf(other);
    }

    public bool IsProperSupersetOf(IEnumerable<PositionLocation> other)
    {
        return _set.IsProperSupersetOf(other);
    }

    public bool IsSubsetOf(IEnumerable<PositionLocation> other)
    {
        return _set.IsSubsetOf(other);
    }

    public bool IsSupersetOf(IEnumerable<PositionLocation> other)
    {
        return _set.IsSupersetOf(other);
    }

    public bool Overlaps(IEnumerable<PositionLocation> other)
    {
        return _set.Overlaps(other);
    }

    public bool SetEquals(IEnumerable<PositionLocation> other)
    {
        return _set.SetEquals(other);
    }

    #endregion
}
