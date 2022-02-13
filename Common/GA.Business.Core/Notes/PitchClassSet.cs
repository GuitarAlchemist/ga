namespace GA.Business.Core.Notes;

using System.Collections;
using System.Collections.Immutable;

using Primitives;

public class PitchClassSet : IReadOnlySet<PitchClass>
{
    private readonly ImmutableHashSet<PitchClass> _set;

    public PitchClassSet(IEnumerable<PitchClass> pitchClasses)
    {
        if (pitchClasses == null) throw new ArgumentNullException(nameof(pitchClasses));
        _set = new HashSet<PitchClass>(pitchClasses).ToImmutableHashSet();
    }

    public int GetIdentity()
    {
        var result = 0;
        var index = 0;
        foreach (var pitchClass in PitchClass.All)
        {
            var weight = 1 << index++;
            if (_set.Contains(pitchClass)) result += weight;
        }

        return result;
    }

    public override string ToString() => @$"{{{string.Join(", ", _set)}}}";

    #region IReadOnlySet members

    public IEnumerator<PitchClass> GetEnumerator() => _set.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _set).GetEnumerator();
    public int Count => _set.Count;
    public bool Contains(PitchClass item) => _set.Contains(item);
    public bool IsProperSubsetOf(IEnumerable<PitchClass> other) => _set.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<PitchClass> other) => _set.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<PitchClass> other) => _set.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<PitchClass> other) => _set.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<PitchClass> other) => _set.Overlaps(other);
    public bool SetEquals(IEnumerable<PitchClass> other) => _set.SetEquals(other);

    #endregion
}