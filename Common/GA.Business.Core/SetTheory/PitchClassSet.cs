namespace GA.Business.Core.SetTheory;

using GA.Core;
using Notes;

// TODO
public class ForteNumber
{

}

// TODO
/// <summary>
/// An unordered collection of notes, without regards to with octave, what order they are played, reduced to its prime form by transposition
/// </summary>
public class SetClass
{
    private static readonly Lazy<IReadOnlyCollection<SetClass>> _lazyObjects;

    public SetClass(PitchClassSetIdentity identity)
    {
        Identity = identity;
    }

    public PitchClassSetIdentity Identity { get; }
}

/// <summary>
/// Represents tones of a scale as a collection of <see cref="PitchClass"/>
/// </summary>
/// <remarks>
/// Example:
/// Dorian scale (See https://ianring.com/musictheory/scales/1709)
///    Pitch class set = {0,2,3,5,7,9,10}
///
/// See https://harmoniousapp.net/p/0b/Clocks-Pitch-Classes
/// </remarks>
[PublicAPI]
public class PitchClassSet : IReadOnlySet<PitchClass>,
                             IMusicObjectCollection<PitchClassSet>
{
    public static IEnumerable<PitchClassSet> Objects => PitchClassSetIdentity.Objects.Select(identity => identity.PitchClassSet);
    public static ImmutableHashSet<PitchClassSet> PrimeForms() => _primeForms.Value;

    public static PitchClassSet FromIdentity(PitchClassSetIdentity identity)
    {
        var pitchClasses = new List<PitchClass>();
        var value = identity.Value;
        foreach (var pitchClass in PitchClass.Items)
        {
            var containsPitchClass = (value & 1) == 1;
            if (containsPitchClass) pitchClasses.Add(pitchClass);
            value >>= 1;
        }

        var result = new PitchClassSet(pitchClasses);

        return result;
    }

    public static implicit operator PitchClassSet(PitchClassSetIdentity identity) => FromIdentity(identity);

    private static readonly Lazy<ILookup<IntervalClassVector, PitchClassSet>> _lazySetsByIntervalContent;
    private static readonly Lazy<ImmutableHashSet<PitchClassSet>> _primeForms;
    private readonly IReadOnlySet<PitchClass> _pitchClasses;

    static PitchClassSet()
    {
        _lazySetsByIntervalContent = new(() => Objects.ToLookup(set => set.IntervalClassVector));
        _primeForms = new(GetPrimeForms);

        static ImmutableHashSet<PitchClassSet> GetPrimeForms() => _lazySetsByIntervalContent.Value
            .Select(grouping => grouping.MinBy(set => set.Identity.Value)!)
            .OrderBy(set => set.Identity.Value)
            .ToImmutableHashSet();
    }

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
    public PrintableReadOnlyCollection<Note.Chromatic> Notes => GetNotes().AsPrintable();
    public IntervalClassVector IntervalClassVector => new(Notes);
    public IReadOnlyCollection<PitchClassSet> Transpositions => _lazySetsByIntervalContent.Value[IntervalClassVector].ToImmutableList();
    public bool IsModal => ModalFamily.ModalIntervalVectors.Contains(IntervalClassVector);
    public bool IsPrimeForm => _primeForms.Value.Contains(this);

    public override string ToString() => Identity.ToString();

    // ReSharper disable once InconsistentNaming
    private PitchClassSetIdentity GetIdentity()
    {
        var value = 0;
        var index = 0;
        // ReSharper disable once LoopCanBeConvertedToQuery
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
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_pitchClasses).GetEnumerator();
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