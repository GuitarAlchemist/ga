namespace GA.Business.Core.Atonal;

using Primitives;
using Notes;

/// <summary>
/// Represents a distinct ordered set of pitch classes
/// </summary>
/// <remarks>
/// 4096 pitch class sets capture every possible musical object<br/>
/// <br/>
/// Example:
/// Dorian scale - <see href="https://ianring.com/musictheory/scales/1709">Pitch class set = {0,2,3,5,7,9,10}</see> | <see href="https://harmoniousapp.net/p/0b/Clocks-Pitch-Classes"/><br/><br/>
/// <br/>
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
    public static IReadOnlyCollection<PitchClassSet> Items =>
        PitchClassSetId
            .Items
            .Select(id => id.PitchClassSet)
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

    private static readonly Lazy<ILookup<IntervalClassVector, PitchClassSet>> _lazyIntervalClassVectorGroup;

    static PitchClassSet()
    {
        _lazyIntervalClassVectorGroup = new(() => Items.ToLookup(set => set.IntervalClassVector));
    }

    /// <summary>
    /// Creates a pitch class set from its identity
    /// </summary>
    /// <param name="identity">The <see cref="PitchClassSetIdentity"/></param>
    /// <returns>The <see cref="PitchClassSet"/></returns>
    /// <remarks>
    /// TODO: Deprecate this
    /// </remarks>
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

    public static PitchClassSet FromId(PitchClassSetId id) => id.PitchClassSet;
    
    // public static implicit operator PitchClassSet(PitchClassSetIdentity identity) => FromIdentity(identity);
    
    private readonly ImmutableSortedSet<PitchClass> _pitchClassesSet;

    /// <summary>
    /// Creates a <see cref="PitchClassSet"/> instance for a collection of Pitch Classes
    /// </summary>
    /// <param name="pitchClasses">The <see cref="IEnumerable{PitchClass}"/></param>
    public PitchClassSet(IEnumerable<PitchClass> pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        var pitchClassesSet = pitchClasses as ImmutableSortedSet<PitchClass> ?? pitchClasses.ToImmutableSortedSet();
        _pitchClassesSet = pitchClassesSet;

        Id = PitchClassSetId.FromPitchClasses(pitchClassesSet);
        Identity = PitchClassSetIdentity.FromPitchClasses(pitchClassesSet); // TODO: Deprecate this
        Cardinality = Cardinality.FromValue(pitchClassesSet.Count);
    }
    
    /// <summary>
    /// Gets the name <see cref="string"/>
    /// </summary>
    public string Name => string.Join(" ", _pitchClassesSet);

    /// <summary>
    /// Gets the <see cref="PitchClassSetIdentity"/>
    /// </summary>
    public PitchClassSetIdentity Identity { get; }
    
    /// <summary>
    /// Gets the <see cref="PitchClassSetId"/>
    /// </summary>
    public PitchClassSetId Id { get; }

    /// <summary>
    /// Gets the <see cref="Cardinality"/>
    /// </summary>
    public Cardinality Cardinality { get; }

    /// <summary>
    /// Gets the <see cref="ChromaticNoteSet"/>
    /// </summary>
    public ChromaticNoteSet Notes => Id.Notes;

    /// <summary>
    /// Gets the <see cref="IntervalClassVector"/>
    /// </summary>
    /// <remarks>
    /// All <see cref="TranspositionsAndInversions"/> items have the same <see cref="IntervalClassVector"/>
    /// </remarks>
    public IntervalClassVector IntervalClassVector => _pitchClassesSet.ToIntervalClassVector();

    /// <summary>
    /// Gets the <see cref="IReadOnlyCollection{PitchClassSet}"/>
    /// </summary>
    public IReadOnlyCollection<PitchClassSet> TranspositionsAndInversions => _lazyIntervalClassVectorGroup.Value[IntervalClassVector].ToImmutableList();

    /// <summary>
    /// Gets the <see cref="Nullable{PitchClassSet}"/>
    /// </summary>
    /// <remarks>
    /// By definition, the prime form is the <see cref="PitchClassSet"/> with the most compact representation
    /// </remarks>
    public PitchClassSet? PrimeForm => TranspositionsAndInversions.MinBy(pitchClassSet => pitchClassSet.Identity.Value);

    /// <summary>
    /// Gets a flag that indicates whether this pitch class set is it prime form
    /// </summary>
    public bool IsPrimeForm => PrimeForm != null && Equals(PrimeForm);

    /// <summary>
    /// True if this pitch class set represents a scale, false otherwise
    /// </summary>
    /// <remarks>
    /// A pitch class set must have a root note to represent a scale
    /// </remarks>
    public bool IsScale => Contains(0);

    /// <summary>
    /// True if this pitch class set represents a scale mode, false otherwise
    /// </summary>
    public bool IsModal => ModalFamily.ModalIntervalVectors.Contains(IntervalClassVector);

    /// <summary>
    /// True is this pitch class set is expressed in normal form, false otherwise
    /// </summary>
    public bool IsNormalForm => ToNormalForm().SequenceEqual(this);

    /// <summary>
    /// Gets the normal form <see cref="PitchClassSet"/>
    /// </summary>
    /// <returns>The normal form <see cref="PitchClassSet"/></returns>
    /// <remarks>
    /// The normal form of a pitch class set is defined as the most compact, ascending arrangement of pitch classes starting from the lowest pitch. This method evaluates all rotations and orderings of the pitch classes within the set to determine the smallest interval span that is the most compact.
    /// 
    /// <para>Example of calculating the normal form for a G major triad:</para>
    /// <para>
    /// <strong>Step 1: Assign Numerical Values</strong><br/>
    /// G = 7, B = 11, D = 2
    /// </para>
    /// <para>
    /// <strong>Step 2: Arrange Numerically and Evaluate Rotations</strong><br/>
    /// Original Order: D (2), G (7), B (11)<br/>
    /// Rotation 1: Intervals - 5, 4<br/>
    /// Rotation 2: Intervals - 4, 3<br/>
    /// Rotation 3: Intervals - 3, 5
    /// </para>
    /// <para>
    /// <strong>Step 3: Transpose Rotations and Check Compactness</strong><br/>
    /// Rotation 1: 2, 7, 11 → 0, 5, 9<br/>
    /// Rotation 2: 7, 11, 14 → 0, 4, 7<br/>
    /// Rotation 3: 11, 14, 19 → 0, 3, 8
    /// </para>
    /// <para>
    /// <strong>Conclusion:</strong><br/>
    /// The most compact form, after transposing and evaluating, is Rotation 3 (0, 3, 8) with the smallest interval span (from 0 to 8).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// PitchClassSet triad = new PitchClassSet(new int[] { 7, 11, 2 });
    /// PitchClassSet normalForm = triad.ToNormalForm();
    /// Console.WriteLine(normalForm);  // Outputs: {0, 3, 8}
    /// </code>
    /// </example>
    public PitchClassSet ToNormalForm()
    {
        var normalForm = new List<PitchClass>();
        var minInterval = int.MaxValue;
        var rotations = GenerateRotations(this).ToImmutableArray();

        foreach (var rotation in rotations)
        {
            var intervalVector = CalculateIntervals(rotation);
            var intervalSpan = intervalVector.Max() - intervalVector.Min();
            if (intervalSpan < minInterval)
            {
                minInterval = intervalSpan; // Reset min interval
                normalForm = [.. rotation];
                continue;
            }

            if (intervalSpan == minInterval
                &&
                IsMoreCompact(intervalVector, CalculateIntervals(normalForm)))
            {
                normalForm = [.. rotation];
            }
        }

        var result = new PitchClassSet(normalForm);

        return result;

        static ImmutableArray<int> CalculateIntervals(IReadOnlyList<PitchClass> pitchClasses)
        {
            var intervals = ImmutableArray.CreateBuilder<int>();
            for (var i = 0; i < pitchClasses.Count; i++)
            {
                var nextIndex = (i + 1) % pitchClasses.Count; // Wraps around to the start
                var interval = (pitchClasses[nextIndex] - pitchClasses[i]).Value;
                intervals.Add(interval);
            }
            return intervals.ToImmutable();
        }

        static IEnumerable<ImmutableSortedSet<PitchClass>> GenerateRotations(PitchClassSet pitchClassSet)
        {
            var builder = ImmutableSortedSet.CreateBuilder<PitchClass>();
            foreach (var basePitchClass in pitchClassSet)
            {
                builder.Clear();
                foreach (var pitchClass in pitchClassSet)
                {
                    builder.Add(pitchClass - basePitchClass);
                }
                yield return builder.ToImmutable();
            }
        }

        static bool IsMoreCompact(IEnumerable<int> vector1, IEnumerable<int> vector2) =>
            vector1
                .Zip(vector2, (v1, v2) => v1.CompareTo(v2))
                .FirstOrDefault(cmp => cmp != 0) < 0;
    }

    /// <inheritdoc />
    public override string ToString() => Name;

    // ReSharper disable once InconsistentNaming
    private ImmutableList<Note.Chromatic> GetNotes()
    {
        // TODO: This looks wrong
        
        var result =
            _pitchClassesSet
                .Select(pitchClass => new Note.Chromatic(pitchClass))
                .ToImmutableList();

        return result;
    }
}