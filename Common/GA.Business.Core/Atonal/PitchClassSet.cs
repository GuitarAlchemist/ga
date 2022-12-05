namespace GA.Business.Core.Atonal;

using GA.Core.Collections;
using Primitives;
using Notes;
using GA.Core.Extensions;

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
public sealed class PitchClassSet : IStaticEnumerable<PitchClassSet>,
                                    IReadOnlySet<PitchClass>,
                                    IComparable<PitchClassSet>
{
    #region IStaticEnumerable<PitchClassSet> Members

    public static IEnumerable<PitchClassSet> Items => PitchClassSetIdentity.Items.Select(identity => identity.PitchClassSet);

    #endregion

    #region Relational Members

    public int CompareTo(PitchClassSet? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (ReferenceEquals(null, other)) return 1;
        return Identity.CompareTo(other.Identity);
    }

    public static bool operator <(PitchClassSet? left, PitchClassSet? right) => Comparer<PitchClassSet>.Default.Compare(left, right) < 0;
    public static bool operator >(PitchClassSet? left, PitchClassSet? right) => Comparer<PitchClassSet>.Default.Compare(left, right) > 0;
    public static bool operator <=(PitchClassSet? left, PitchClassSet? right) => Comparer<PitchClassSet>.Default.Compare(left, right) <= 0;
    public static bool operator >=(PitchClassSet? left, PitchClassSet? right) => Comparer<PitchClassSet>.Default.Compare(left, right) >= 0;

    #endregion

    #region Equality Members

    private bool Equals(PitchClassSet other) => Identity.Equals(other.Identity);
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is PitchClassSet other && Equals(other);
    public override int GetHashCode() => Identity.GetHashCode();
    public static bool operator ==(PitchClassSet? left, PitchClassSet? right) => Equals(left, right);
    public static bool operator !=(PitchClassSet? left, PitchClassSet? right) => !Equals(left, right);

    #endregion

    #region IReadOnlySet members

    public IEnumerator<PitchClass> GetEnumerator() => _pitchClassesSet.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_pitchClassesSet).GetEnumerator();
    public int Count => _pitchClassesSet.Count;
    public bool Contains(PitchClass item) => _pitchClassesSet.Contains(item);
    public bool IsProperSubsetOf(IEnumerable<PitchClass> other) => _pitchClassesSet.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<PitchClass> other) => _pitchClassesSet.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<PitchClass> other) => _pitchClassesSet.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<PitchClass> other) => _pitchClassesSet.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<PitchClass> other) => _pitchClassesSet.Overlaps(other);
    public bool SetEquals(IEnumerable<PitchClass> other) => _pitchClassesSet.SetEquals(other);

    #endregion


    public static ImmutableHashSet<PitchClassSet> PrimeForms => _primeForms.Value;

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

    public static PitchClassSet FromNotes(IEnumerable<Note> notes)
    {
        var pitchClasses =
            notes.Select(note => note.PitchClass)
                .ToImmutableArray();

        var result = new PitchClassSet(pitchClasses);

        return result;
    }

    public static PitchClassSet FromNotes(params Note[] notes) => FromNotes(notes.AsEnumerable());

    public static implicit operator PitchClassSet(PitchClassSetIdentity identity) => FromIdentity(identity);

    private static readonly Lazy<ILookup<(Cardinality, IntervalClassVector), PitchClassSet>> _lazyTranspositions;
    private static readonly Lazy<ImmutableHashSet<PitchClassSet>> _primeForms;
    private readonly ImmutableSortedSet<PitchClass> _pitchClassesSet;

    static PitchClassSet()
    {
        _lazyTranspositions = new(() => Items.ToLookup(set => (set.Cardinality, set.IntervalClassVector)));
        _primeForms = new(GetPrimeForms);

        static ImmutableHashSet<PitchClassSet> GetPrimeForms() => _lazyTranspositions.Value
            .Select(grouping => grouping.MinBy(set => set.Identity.Value)!)
            .OrderBy(set => set.Identity.Value)
            .ToImmutableHashSet();
    }

    public PitchClassSet(
        IReadOnlyCollection<PitchClass> pitchClasses,
        PitchClassSetOrder order = default)
    {
        if (pitchClasses == null) throw new ArgumentNullException(nameof(pitchClasses));
        var distinctPitchClasses = pitchClasses.Distinct().ToImmutableList();

        ImmutableSortedSet<PitchClass> pitchClassesSet;
        if (order == PitchClassSetOrder.ScaleOrder)
        {
            var firstPitchClass = distinctPitchClasses.First();
            pitchClassesSet = distinctPitchClasses.Select(pc => PitchClass.FromValue((pc.Value - firstPitchClass.Value) % 12)).ToImmutableSortedSet();
        }
        else
        {
            pitchClassesSet = distinctPitchClasses.ToImmutableSortedSet();
        }

        _pitchClassesSet = pitchClassesSet;
        Identity = GetIdentity(pitchClassesSet);
        Cardinality = Cardinality.FromValue(pitchClassesSet.Count);

        // ReSharper disable once InconsistentNaming
        static PitchClassSetIdentity GetIdentity(IReadOnlySet<PitchClass> pitchClassesSet)
        {
            var value = 0;
            var index = 0;
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var pitchClass in PitchClass.Items)
            {
                var weight = 1 << index++;
                if (pitchClassesSet.Contains(pitchClass)) value += weight;
            }

            return value;
        }
    }

    public PitchClassSetIdentity Identity { get; }
    public Cardinality Cardinality { get; }
    public IReadOnlyCollection<Note.Chromatic> Notes => GetNotes().AsPrintable();
    public IntervalClassVector IntervalClassVector => _pitchClassesSet.ToIntervalClassVector();
    public IReadOnlyCollection<PitchClassSet> Transpositions => _lazyTranspositions.Value[(Cardinality, IntervalClassVector)].ToImmutableList();
    public bool IsModal => ModalFamily.ModalIntervalVectors.Contains(IntervalClassVector);
    public bool IsPrimeForm => _primeForms.Value.Contains(this);
    private PitchClassSet PrimeForm => Transpositions.First(set => set.IsPrimeForm);

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('(');
        sb.Append(string.Join(" ", _pitchClassesSet));
        sb.Append(')');
        return sb.ToString();
    }

    // ReSharper disable once InconsistentNaming
    private IReadOnlyCollection<Note.Chromatic> GetNotes()
    {
        var result =
            _pitchClassesSet.Select(pitchClass => new Note.Chromatic(pitchClass))
                .ToImmutableList();

        return result;
    }
}