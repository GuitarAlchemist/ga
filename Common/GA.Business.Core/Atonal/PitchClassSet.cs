namespace GA.Business.Core.Atonal;

using Primitives;
using Notes;

/// <summary>
/// Represents a set of pitch classes
/// </summary>
/// <remarks>
/// Example:
/// Dorian scale (See https://ianring.com/musictheory/scales/1709)
///    Pitch class set = {0,2,3,5,7,9,10}
///
/// See https://harmoniousapp.net/p/0b/Clocks-Pitch-Classes
/// "
/// 4096 pitch class sets capture every possible musical object,
/// and let us see some important connections between objects by visualizing any chord or scale on a circle
/// "
/// </remarks>
[PublicAPI]
public sealed class PitchClassSet : IStaticReadonlyCollection<PitchClassSet>,
                                    IParsable<PitchClassSet>,
                                    IReadOnlySet<PitchClass>,
                                    IComparable<PitchClassSet>
{
    #region IStaticEnumerable<PitchClassSet> Members

    /// <summary>
    /// Gets all 4096 possible pitch class sets (See https://harmoniousapp.net/p/0b/Clocks-Pitch-Classes)
    /// <br/><see cref="IReadOnlyCollection{PitchClassSet}"/>
    /// </summary>
    public static IReadOnlyCollection<PitchClassSet> Items => PitchClassSetIdentity.Items
        .Select(identity => identity.PitchClassSet)
        .ToLazyCollection();

    #endregion
    
    #region IParsable<PitchClassSet> Members

    public static PitchClassSet Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, null, out var result)) throw new PitchClassSetParseException();
        return result;
    }

    /// <inheritdoc />
    public static bool TryParse(string? s, IFormatProvider? provider, out PitchClassSet result)
    {
        ArgumentNullException.ThrowIfNull(s);
        
        result = null!;
        var segments = s.Select(c => c.ToString());
        var pitchClasses = new List<PitchClass>();
        foreach (var segment in segments)
        {
            if (!PitchClass.TryParse(segment, null, out var pitchClass)) return false; // Fail if one item fails parsing
            pitchClasses.Add(pitchClass);
        }

        // Success
        result = new(pitchClasses);
        return true;
    }
   
    #endregion

    #region Relational Members

    public int CompareTo(PitchClassSet? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        return other is null ? 1 : Identity.CompareTo(other.Identity);
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

    public IEnumerator<PitchClass> GetEnumerator() => _orderedPitchClasses.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_orderedPitchClasses).GetEnumerator();
    public int Count => _orderedPitchClasses.Count;
    public bool Contains(PitchClass item) => _orderedPitchClasses.Contains(item);
    public bool IsProperSubsetOf(IEnumerable<PitchClass> other) => _orderedPitchClasses.IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<PitchClass> other) => _orderedPitchClasses.IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<PitchClass> other) => _orderedPitchClasses.IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<PitchClass> other) => _orderedPitchClasses.IsSupersetOf(other);
    public bool Overlaps(IEnumerable<PitchClass> other) => _orderedPitchClasses.Overlaps(other);
    public bool SetEquals(IEnumerable<PitchClass> other) => _orderedPitchClasses.SetEquals(other);

    #endregion

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

    private static readonly Lazy<ILookup<IntervalClassVector, PitchClassSet>> _lazyTranspositionsAndInversions;
    private readonly ImmutableSortedSet<PitchClass> _orderedPitchClasses;

    static PitchClassSet()
    {
        _lazyTranspositionsAndInversions = new(() => Items.ToLookup(set => set.IntervalClassVector));
    }

    public PitchClassSet(IEnumerable<PitchClass> pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        var pitchClassesSet = pitchClasses.ToImmutableSortedSet();
        _orderedPitchClasses = pitchClassesSet;
        
        Identity = PitchClassSetIdentity.FromPitchClasses(pitchClassesSet);
        Cardinality = Cardinality.FromValue(pitchClassesSet.Count);
    }

    /// <summary>
    /// Gets the <see cref="PitchClassSetIdentity"/>
    /// </summary>
    public PitchClassSetIdentity Identity { get; }
    
    /// <summary>
    /// Gets the <see cref="Cardinality"/>
    /// </summary>
    public Cardinality Cardinality { get; }
    
    public IReadOnlyCollection<Note.Chromatic> Notes => GetNotes().AsPrintable();
    
    /// <summary>
    /// Gets the <see cref="IntervalClassVector"/>
    /// </summary>
    /// <remarks>
    /// All <see cref="TranspositionsAndInversions"/> items have the same <see cref="IntervalClassVector"/>
    /// </remarks>
    public IntervalClassVector IntervalClassVector => _orderedPitchClasses.ToIntervalClassVector();
    
    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{PitchClassSet}"/>
    /// </summary>
    public IReadOnlyCollection<PitchClassSet> TranspositionsAndInversions => _lazyTranspositionsAndInversions.Value[IntervalClassVector].ToImmutableList();

    /// <summary>
    /// Gets the <see cref="Nullable{PitchClassSet}"/>
    /// </summary>
    /// <remarks>
    /// By definition, the prime form is the <see cref="PitchClassSet"/> with the most compact representation
    /// </remarks>
    private PitchClassSet? PrimeForm => TranspositionsAndInversions.MinBy(pitchClassSet => pitchClassSet.Identity.Value);
    
    public bool IsPrimeForm => PrimeForm != null && Equals(PrimeForm);
    
    public bool IsModal => ModalFamily.ModalIntervalVectors.Contains(IntervalClassVector);
    
    /// <inheritdoc />
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('(');
        sb.Append(string.Join(" ", _orderedPitchClasses));
        sb.Append(')');
        return sb.ToString();
    }

    private string GetName()
    {
        var sb = new StringBuilder();
        sb.Append('(');
        sb.Append(string.Join(" ", _orderedPitchClasses));
        sb.Append(')');
        return sb.ToString();
    }
    
    // ReSharper disable once InconsistentNaming
    private ImmutableList<Note.Chromatic> GetNotes()
    {
        var result =
            _orderedPitchClasses
                .Select(pitchClass => new Note.Chromatic(pitchClass))
                .ToImmutableList();

        return result;
    }
}