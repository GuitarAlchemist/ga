namespace GA.Business.Core.Atonal;

using Intervals.Primitives;
using Primitives;
using Notes;
using Notes.Primitives;
using Scales;
using Tonal;
using System.Linq;
using GA.Core.Collections;

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
    #region IStaticReadonlyCollection<PitchClassSet> Members

    /// <summary>
    /// Gets all possible set classes (<see href="https://harmoniousapp.net/p/71/Set-Classes"/>)
    /// <br/><see cref="IReadOnlyCollection{PitchClassSet}"/>
    /// </summary>
    public static IReadOnlyCollection<PitchClassSet> Items => AllPitchClassSets.Instance;

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
        return other is null ? 1 : Id.CompareTo(other.Id);
    }

    public static bool operator <(PitchClassSet? left, PitchClassSet? right) => Comparer<PitchClassSet>.Default.Compare(left, right) < 0;
    public static bool operator >(PitchClassSet? left, PitchClassSet? right) => Comparer<PitchClassSet>.Default.Compare(left, right) > 0;
    public static bool operator <=(PitchClassSet? left, PitchClassSet? right) => Comparer<PitchClassSet>.Default.Compare(left, right) <= 0;
    public static bool operator >=(PitchClassSet? left, PitchClassSet? right) => Comparer<PitchClassSet>.Default.Compare(left, right) >= 0;

    #endregion

    #region Equality Members

    private bool Equals(PitchClassSet other) => Id.Equals(other.Id);
    public override bool Equals(object? obj) => ReferenceEquals(this, obj) || obj is PitchClassSet other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
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

    public static PitchClassSet FromId(PitchClassSetId id) => id.ToPitchClassSet();

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
        Cardinality = Cardinality.FromValue(pitchClassesSet.Count);
    }

    /// <summary>
    /// Gets the name <see cref="string"/>
    /// </summary>
    public string Name => string.Join(" ", _pitchClassesSet);

    ///// <summary>
    ///// Gets the <see cref="PitchClassSetIdentity"/>
    ///// </summary>
    //public PitchClassSetIdentity Identity { get; }

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
    /// Gets the <see cref="Nullable{ModalFamily}"/>
    /// </summary>
    public ModalFamily? ModalFamily => ModalFamily.TryGetValue(IntervalClassVector, out var modalFamily) ? modalFamily : null;

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
    public PitchClassSet? PrimeForm => TranspositionsAndInversions.MinBy(pitchClassSet => pitchClassSet.Id.Value);

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

    public bool IsClusterFree => Id.IsClusterFree;

    /// <summary>
    /// Gets the complements <see cref="PitchClassSet"/>
    /// </summary>
    public PitchClassSet Complement => FromId(Id.Complement);

    /// <summary>
    /// Gets the inverse <see cref="PitchClassSet"/>
    /// </summary>
    public PitchClassSet Inverse => FromId(Id.Inverse);

    public Uri? ScaleVideoUrl => ScaleVideoUrlById.Get(Id);
    public Uri ScalePageUrl => new($"https://ianring.com/musictheory/scales/{Id.Value}");

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

    public Key ClosestDiatonicKey => FindClosestDiatonicKey2() ?? Key.Major.C;

    public PrintableReadOnlyCollection<Note.Accidented> GetDiatonicNotes()
    {
        var key = ClosestDiatonicKey;
        var noteByPitchClass = key.Notes.ToDictionary(note => note.PitchClass, note => note);

        var notes = new List<Note.Accidented>();
        var usedNaturalNotes = new HashSet<NaturalNote>();
        foreach (var pitchClass in _pitchClassesSet)
        {
            if (noteByPitchClass.TryGetValue(pitchClass, out var keyNote))
            {
                var note = keyNote.ToAccidented();
                usedNaturalNotes.Add(note.NaturalNote);
                notes.Add(note);
            }
            else
            {
                var closestNote = FindClosestDiatonicNoteWithAccidental(IsModal, usedNaturalNotes, pitchClass, key.Notes);
                notes.Add(closestNote);
            }
        }

        var result = notes.ToImmutableList().AsPrintable();

        return result;
    }

    private Note.Accidented FindClosestDiatonicNoteWithAccidental(bool isModal,
        HashSet<NaturalNote> usedNaturalNotes,
        PitchClass target,
        IReadOnlyCollection<Note.KeyNote> keyNotes)
    {
        Note.Accidented? closestNote = null;
        var smallestDifference = int.MaxValue;
        foreach (var keyNote in keyNotes)
        {
            var difference = Math.Abs((int)keyNote.PitchClass - (int)target);
            if (difference >= smallestDifference) continue;
            smallestDifference = difference;
            closestNote = keyNote;
        }

        if (closestNote == null) throw new InvalidOperationException("No closest diatonic note found.");

        // Determine the accidental
        var accidentalValue = (int)target - (int)closestNote.PitchClass;
        var accidental =
            closestNote.Accidental.HasValue
                ? closestNote.Accidental.Value + accidentalValue
                : (Accidental)accidentalValue;
        var result = new Note.Accidented(closestNote.NaturalNote, accidental);

        if (isModal && usedNaturalNotes.Contains(result.NaturalNote))
        {
            var candidate = result;
            var orderedAvailableEnharmonics =
                Note.Accidented.Items
                    .Where(note =>
                        note.PitchClass == candidate.PitchClass
                        &&
                        note.NaturalNote != candidate.NaturalNote
                        &&
                        !usedNaturalNotes.Contains(note.NaturalNote)
                        )
                    .OrderBy(note => Math.Abs(note.Accidental?.Value ?? 0))
                    .ToImmutableArray();
            result = orderedAvailableEnharmonics.FirstOrDefault() ?? result;
        }

        return result;
    }

    /// <inheritdoc />
    public override string ToString() => Name;

    private Key? FindClosestDiatonicKey()
    {
        var normalForm =
            IsNormalForm ?
                this :
            ToNormalForm();
        // Determine if the pitch class set likely represents a minor scale/chord
        var containsMinorThird = normalForm.Contains(Note.Chromatic.DSharpOrEFlat.PitchClass);
        var expectedKeyMode = containsMinorThird ? KeyMode.Minor : KeyMode.Major;

        var keyMatches = new List<(Key Key, int CommonPitchClasses)>();
        // Compare pitch class set with each key
        foreach (var key in Key.Items)
        {
            var commonPitchClasses = this.Intersect(key.PitchClassSet).Count();
            keyMatches.Add((key, commonPitchClasses));
        }

        // Find the maximum number of common pitch classes
        var maxCommonPitchClasses = keyMatches.Select(tuple => tuple.CommonPitchClasses).Max();

        // Filter and sort candidate keys
        var candidateKeys = keyMatches
            .Where(tuple => tuple.CommonPitchClasses == maxCommonPitchClasses)
            .Select(tuple => tuple.Key)
            .OrderByDescending(key => key.KeyMode == expectedKeyMode) // prioritize expected key mode
            .ThenBy(key => key.KeySignature.AccidentalCount)          // then by fewer accidentals
            .ThenByDescending(key => key.KeySignature.IsSharpKey)     // prefer sharp keys if tied
            .ThenBy(key => key.KeyMode)                               // then by key mode
            .ToImmutableList();

        return candidateKeys.FirstOrDefault();
    }

    private Key? FindClosestDiatonicKey2()
    {
        var dict = new Dictionary<Key, IReadOnlyCollection<PitchClass>>();
        foreach (var key in Key.Items)
        {
            var accidentedKeyNotes = key.Notes.Where(note => note.Accidental != null);
            var accidentedPitchClasses= accidentedKeyNotes.Select(note => note.PitchClass).ToImmutableArray();

            dict.Add(key, accidentedPitchClasses);
        }

        // Find the closest key
        var normalForm =
            IsNormalForm ?
                this :
                ToNormalForm();
        // Determine if the pitch class set likely represents a minor scale/chord
        var containsMinorThird = normalForm.Contains(Note.Chromatic.DSharpOrEFlat.PitchClass);

        var expectedKeyMode = containsMinorThird ? KeyMode.Minor : KeyMode.Major;
        var closestKey = IdentifyClosestKey(this, dict.AsReadOnly(), expectedKeyMode);

        return closestKey ?? Key.Major.C;
    }

    private Key? IdentifyClosestKey(
        PitchClassSet normalForm,
        IReadOnlyDictionary<Key, IReadOnlyCollection<PitchClass>> items,
        KeyMode expectedKeyMode)
    {
        var list = new List<(Key Key, PrintableReadOnlyCollection<Note.KeyNote> Matches, PrintableReadOnlyCollection<PitchClass>)>();
        foreach (var (key, _) in items)
        {
            var matches = new List<Note.KeyNote>();
            var keyNotes = key.Notes.ToImmutableList();
            foreach (var keyNote in keyNotes)
            {
                if (normalForm.Contains(keyNote.PitchClass)) matches.Add(keyNote);
            }

            var pMatches = matches.AsReadOnly().AsPrintable();
            var pPitchClasses = matches.Select(note => note.PitchClass).OrderBy(pitchClass => pitchClass).ToImmutableList().AsPrintable();
            list.Add((key, pMatches, pPitchClasses));
        }

        // Result
        if (list.Count == 0) return null;

        var result =
            list
                .OrderByDescending(tuple => tuple.Matches.Count)
                .ThenByDescending(tuple => tuple.Key.KeyMode == expectedKeyMode) // prioritize expected key mode
                .First()
                .Key;

        return result;
    }

    #region Innner Classes
    
    private class AllPitchClassSets : LazyCollectionBase<PitchClassSet>
    {
        public static readonly AllPitchClassSets Instance = new();
        private AllPitchClassSets() : base(Collection, separator: ", ") { }
        private static IEnumerable<PitchClassSet> Collection => PitchClassSetId.Items.Select(id => id.ToPitchClassSet());
    }    
    
    #endregion
}