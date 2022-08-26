namespace GA.Business.Core.Notes;

using System.Collections;
using System.Collections.Immutable;

using Primitives;

public class PitchClassSet : IReadOnlySet<PitchClass>
{
    public static PitchClassSet FromIdentity(int identity)
    {
        var hashset = new HashSet<PitchClass>();
        foreach (var pitchClass in PitchClass.Items)
        {
            if ((identity & 1) == 1) hashset.Add(pitchClass);
            identity = identity >> 1;
        }

        var result = new PitchClassSet(hashset);

        return result;
    }

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
        foreach (var pitchClass in PitchClass.Items)
        {
            var weight = 1 << index++;
            if (_set.Contains(pitchClass)) result += weight;
        }

        return result;
    }

    public IReadOnlyCollection<Note.Chromatic> GetNotes()
    {
        var result = 
            _set.Select(pitchClass => new Note.Chromatic(pitchClass))
                .ToImmutableList();

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