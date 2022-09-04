namespace GA.Business.Core.Notes;

using Primitives;
using Atonal;
using System.Collections.Generic;

/// <summary>
/// Represents the tones in this scale as a collection of <see cref="PitchClass"/>
/// </summary>
/// <remarks>
/// Example:
/// Dorian scale (See https://ianring.com/musictheory/scales/1709)
///    Pitch class set = {0,2,3,5,7,9,10}
/// </remarks>
public class PitchClassSet : IReadOnlySet<PitchClass>
{
    public static PitchClassSet FromIdentity(PitchClassSetIdentity identity)
    {
        var pitchClasses = new List<PitchClass>();
        var identityValue = identity.Value;
        foreach (var pitchClass in PitchClass.Items)
        {
            if (PitchClassSetIdentity.IsValid(identityValue)) pitchClasses.Add(pitchClass);
            identityValue >>= 1;
        }

        var result = new PitchClassSet(pitchClasses);

        return result;
    }

    public static IEnumerable<PitchClassSet> Enumerate()
    {
        var identities = PitchClassSetIdentity.ValidIdentities();
        foreach (var identity in identities)
        {
            yield return FromIdentity(identity);
        }
    }

    private readonly IReadOnlySet<PitchClass> _pitchClasses;

    public PitchClassSet(
        IReadOnlyCollection<PitchClass> pitchClasses, 
        bool normalize = false)
    {
        if (pitchClasses == null) throw new ArgumentNullException(nameof(pitchClasses));
        var distinctPitchClasses = pitchClasses.Distinct().ToImmutableList();

        if (normalize)
        {
            var firstPitchClass = distinctPitchClasses.First();
            _pitchClasses = distinctPitchClasses.Select(pc => PitchClass.FromValue((pc.Value - firstPitchClass.Value) % 12)).ToImmutableSortedSet();
        }
        else
        {
            _pitchClasses = distinctPitchClasses.ToImmutableSortedSet();
        }
    }

    public PitchClassSetIdentity Identity => GetIdentity();
    public IReadOnlyCollection<Note.Chromatic> Notes => GetNotes();
    public IntervalVector IntervalVector => new(Notes);
    public bool IsModal => ModalFamily.ModalIntervalVectors.Contains(IntervalVector);
    public ModalFamily? ModalFamily
    {
        get
        {
            if (ModalFamily.TryGetValue(IntervalVector, out var modalFamily)) return modalFamily;
            return null;
        }
    }

    public override string ToString() => @$"{{{string.Join(",", _pitchClasses)}}} ({GetIdentity().Value})";

    // ReSharper disable once InconsistentNaming
    private PitchClassSetIdentity GetIdentity()
    {
        var value = 0;
        var index = 0;
        foreach (var pitchClass in PitchClass.Items)
        {
            var weight = 1 << index++;
            if (_pitchClasses.Contains(pitchClass)) value += weight;
        }

        return new(value);
    }

    // ReSharper disable once InconsistentNaming
    private IReadOnlyCollection<Note.Chromatic> GetNotes()
    {
        var result = 
            _pitchClasses.Select(pitchClass => new Note.Chromatic(pitchClass))
                .ToImmutableList();

        return result;
    }


    #region IReadOnlySet members

    public IEnumerator<PitchClass> GetEnumerator() => _pitchClasses.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _pitchClasses).GetEnumerator();
    public int Count => _pitchClasses.Count;
    public bool Contains(PitchClass item) => _pitchClasses.Contains(item);
    public bool IsProperSubsetOf(IEnumerable<PitchClass> other) => _pitchClasses.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<PitchClass> other) => _pitchClasses.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<PitchClass> other) => _pitchClasses.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<PitchClass> other) => _pitchClasses.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<PitchClass> other) => _pitchClasses.Overlaps(other);
    public bool SetEquals(IEnumerable<PitchClass> other) => _pitchClasses.SetEquals(other);

    #endregion
}