namespace GA.Domain.Core.Theory.Atonal;

using Design.Attributes;
using Design.Schema;
using GA.Core.Collections;
using GA.Core.Collections.Abstractions;
using Primitives.Notes;
using Tonal;
using Tonal.Scales;
using KeyMode = Tonal.KeyMode;

/// <summary>
///     Represents a distinct ordered set of pitch classes
/// </summary>
/// <remarks>
///     4096 pitch class sets capture every possible musical object<br />
///     <br />
///     Example:
///     Dorian scale - <see href="https://ianring.com/musictheory/scales/1709">Pitch class set = {0,2,3,5,7,9,10}</see> |
///     <see href="https://harmoniousapp.net/p/0b/Clocks-Pitch-Classes" /><br /><br />
///     <br />
/// </remarks>
[PublicAPI]
[DomainInvariant("Pitch class set must be a valid subset of 12-tone chromatic scale",
    "Cardinality >= 0 && Cardinality <= 12")]
[DomainRelationship(typeof(IntervalClassVector), RelationshipType.IsChildOf)]
[DomainRelationship(typeof(ModalFamily), RelationshipType.IsChildOf)]
public sealed class PitchClassSet : IStaticReadonlyCollection<PitchClassSet>,
    IParsable<PitchClassSet>,
    IReadOnlySet<PitchClass>,
    IComparable<PitchClassSet>
{
    private static readonly Lazy<ILookup<IntervalClassVector, PitchClassSet>> _lazyIntervalClassVectorGroup;

    // public static implicit operator PitchClassSet(PitchClassSetIdentity identity) => FromIdentity(identity);

    private readonly ImmutableSortedSet<PitchClass> _pitchClassesSet;

    /// <summary>
    ///     Gets the <see cref="IReadOnlyCollection{PitchClassSet}" />
    /// </summary>
    private IReadOnlyCollection<PitchClassSet>? _transpositionsAndInversions;

    static PitchClassSet() => _lazyIntervalClassVectorGroup = new(() => Items.ToLookup(set => set.IntervalClassVector));

    /// <summary>
    ///     Creates a <see cref="PitchClassSet" /> instance for a collection of Pitch Classes
    /// </summary>
    /// <param name="pitchClasses">The <see cref="IEnumerable{PitchClass}" /></param>
    public PitchClassSet(IEnumerable<PitchClass> pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        var pitchClassesSet = pitchClasses as ImmutableSortedSet<PitchClass> ?? [.. pitchClasses];
        _pitchClassesSet = pitchClassesSet;

        var mask = 0;
        foreach (var pitchClass in pitchClassesSet)
        {
            mask |= 1 << (pitchClass.Value & 0xF);
        }

        PitchClassMask = mask;

        Id = PitchClassSetId.FromPitchClasses(pitchClassesSet);
        Cardinality = Cardinality.FromValue(pitchClassesSet.Count);
    }

    #region Modal pitch class sets

    /// <summary>
    ///     All modal pitch class sets
    /// </summary>
    public static IEnumerable<PitchClassSet> ModalItems => Items.Where(set => set.IsModal);

    #endregion

    /// <summary>
    ///     Gets the name <see cref="string" />
    /// </summary>
    public string Name => string.Join(" ", _pitchClassesSet);

    /// <summary>
    ///     Gets the <see cref="PitchClassSetId" />
    /// </summary>
    public PitchClassSetId Id { get; }

    /// <summary>
    ///     Gets the <see cref="Cardinality" />
    /// </summary>
    public Cardinality Cardinality { get; }

    /// <summary>
    ///     Gets the <see cref="ChromaticNoteSet" />
    /// </summary>
    public ChromaticNoteSet Notes => Id.Notes;

    /// <summary>
    ///     Gets the <see cref="IntervalClassVector" />
    /// </summary>
    /// <remarks>
    ///     All <see cref="TranspositionsAndInversions" /> items have the same <see cref="IntervalClassVector" />
    /// </remarks>
    public IntervalClassVector IntervalClassVector => _pitchClassesSet.ToIntervalClassVector();

    public ModalFamily? ModalFamily =>
        ModalFamily.TryGetValue(IntervalClassVector, out var modalFamily) ? modalFamily : null;

    /// <summary>
    ///     The 0-based index of this mode within its modal family (ordered by PitchClassSetId).
    /// </summary>
    public int ModeIndex => ModalFamily?.Modes.IndexOf(this) ?? -1;

    /// <summary>
    ///     The total number of modes in this set's modal family.
    /// </summary>
    public int FamilySize => ModalFamily?.Modes.Count ?? 0;

    public IReadOnlyCollection<PitchClassSet> TranspositionsAndInversions
    {
        get
        {
            if (_transpositionsAndInversions is not null)
            {
                return _transpositionsAndInversions;
            }

            // Generate the 24 forms: 12 rotations (transpositions) and 12 rotations of the inversion
            var values = new HashSet<int>();

            var baseId = Id;
            var invId = Id.Inverse;

            for (var i = 0; i < 12; i++)
            {
                values.Add(baseId.Rotate(i).Value);
                values.Add(invId.Rotate(i).Value);
            }

            // Map back to PitchClassSet instances deterministically ordered by id
            var list = values
                .OrderBy(v => v)
                .Select(v => ((PitchClassSetId)v).ToPitchClassSet())
                .ToImmutableList();

            _transpositionsAndInversions = list;
            return _transpositionsAndInversions;
        }
    }

    /// <summary>
    ///     Gets the transposition/inversion prime form (the OPTIC / set-class representative).
    /// </summary>
    /// <remarks>
    ///     The canonical member of <see cref="TranspositionsAndInversions" /> — the one with the smallest
    ///     packed id. Owned by <see cref="PitchClassSetId.PrimeForm" /> (pure id arithmetic); this property
    ///     just lifts it back to a <see cref="PitchClassSet" />. <b>Not</b> the same as
    ///     <see cref="ToNormalForm" />, which canonicalises by interval-span compactness rather than minimal
    ///     id and can return a different rotation.
    /// </remarks>
    public PitchClassSet? PrimeForm => Id.PrimeForm.ToPitchClassSet();

    /// <summary>
    ///     Gets a flag that indicates whether this pitch class set is it prime form
    /// </summary>
    public bool IsPrimeForm => PrimeForm != null && Equals(PrimeForm);

    /// <summary>
    ///     True if this pitch class set represents a scale, false otherwise
    /// </summary>
    /// <remarks>
    ///     A pitch class set must have a root note to represent a scale
    /// </remarks>
    public bool IsScale => Contains(0);

    /// <summary>
    ///     Bitmask indicating which pitch classes are present (bit n = 1 if pitch class n is included).
    /// </summary>
    public int PitchClassMask { get; }

    /// <summary>
    ///     True if this set belongs to a family with multiple distinct rotations (e.g. Diatonic, Harmonic Minor).
    /// </summary>
    public bool IsMultimodal => FamilySize > 1;

    /// <summary>
    ///     True if this set is the only member of its modal family (e.g. Whole Tone, Diminished).
    /// </summary>
    public bool IsMonomodal => FamilySize == 1;

    /// <summary>
    ///     True if this pitch class set belongs to any modal family (atonal definition).
    /// </summary>
    public bool IsModal => FamilySize > 0;

    /// <summary>
    ///     True if this ICV is shared by set classes that are NOT transpositions/inversions of each other.
    /// </summary>
    public bool IsZRelated
    {
        get
        {
            if (ModalFamily == null || FamilySize <= 1)
            {
                return false;
            }

            var first = ModalFamily.Modes[0];
            return ModalFamily.Modes.Any(m => !first.TranspositionsAndInversions.Any(ti => ti.Id == m.Id));
        }
    }

    /// <summary>
    ///     Calculates the "Center of Gravity" of the set on the chromatic circle.
    ///     Returns a value 0-1 representing the mean angle.
    /// </summary>
    public double CenterOfGravity
    {
        get
        {
            if (Count == 0)
            {
                return 0;
            }

            var x = _pitchClassesSet.Sum(pc => Math.Cos(pc.Value * Math.PI / 6.0));
            var y = _pitchClassesSet.Sum(pc => Math.Sin(pc.Value * Math.PI / 6.0));
            var angle = Math.Atan2(y, x);
            return (angle + Math.PI) / (2 * Math.PI);
        }
    }

    /// <summary>
    ///     Measures the diversity of intervals between adjacent notes (Step Entropy).
    /// </summary>
    public double StepEntropy
    {
        get
        {
            if (Count <= 1)
            {
                return 0;
            }

            var steps = new List<int>();
            var sorted = _pitchClassesSet.Select(pc => pc.Value).OrderBy(v => v).ToList();
            for (var i = 0; i < sorted.Count; i++)
            {
                steps.Add((sorted[(i + 1) % sorted.Count] - sorted[i] + 12) % 12);
            }

            var counts = steps.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
            var entropy = counts.Values.Sum(c =>
            {
                var p = (double)c / steps.Count;
                return -p * Math.Log2(p);
            });
            return entropy / Math.Log2(12); // Normalized
        }
    }

    /// <summary>
    ///     Measures brightness based on the preponderance of large steps.
    /// </summary>
    public double StepBrightness
    {
        get
        {
            if (Count <= 1)
            {
                return 0.5;
            }

            var steps = new List<int>();
            var sorted = _pitchClassesSet.Select(pc => pc.Value).OrderBy(v => v).ToList();
            for (var i = 0; i < sorted.Count; i++)
            {
                steps.Add((sorted[(i + 1) % sorted.Count] - sorted[i] + 12) % 12);
            }

            return steps.Average(s => s) / 6.0; // Very rough proxy
        }
    }

    /// <summary>
    ///     Structural Consonance potential based on IC 3, 4, 5.
    /// </summary>
    public double ConsonancePotential
    {
        get
        {
            var icv = IntervalClassVector;
            var total = icv.Sum();
            if (total == 0)
            {
                return 0;
            }

            return (double)(icv[IntervalClass.FromValue(3)] + icv[IntervalClass.FromValue(4)] +
                            icv[IntervalClass.FromValue(5)]) / total;
        }
    }

    /// <summary>
    ///     Structural Dissonance potential based on IC 1, 2, 6.
    /// </summary>
    public double DissonanceIndex
    {
        get
        {
            var icv = IntervalClassVector;
            var total = icv.Sum();
            if (total == 0)
            {
                return 0;
            }

            return (double)(icv[IntervalClass.FromValue(1)] + icv[IntervalClass.FromValue(2)] +
                            icv[IntervalClass.FromValue(6)]) / total;
        }
    }

    /// <summary>
    ///     True is this pitch class set is expressed in normal form, false otherwise
    /// </summary>
    public bool IsNormalForm => SetTables.NormalFormMasks[Id.Value] == Id.Value;

    public bool IsClusterFree => Id.IsClusterFree;

    /// <summary>
    ///     Gets the complements <see cref="PitchClassSet" />
    /// </summary>
    public PitchClassSet Complement => FromId(Id.Complement);

    /// <summary>
    ///     Gets the inverse <see cref="PitchClassSet" />
    /// </summary>
    public PitchClassSet Inverse => FromId(Id.Inverse);

    /// <summary>
    ///     Gets the M5 transform (multiply pitch classes by 5 mod 12 — the circle-of-fourths transform).
    /// </summary>
    public PitchClassSet M5 => FromId(Id.M5);

    /// <summary>
    ///     Gets the M7 transform (multiply pitch classes by 7 mod 12).
    /// </summary>
    public PitchClassSet M7 => FromId(Id.M7);

    public Uri? ScaleVideoUrl => ScaleVideoUrlById.Get(Id);
    public Uri ScalePageUrl => new($"https://ianring.com/musictheory/scales/{Id.Value}");

    public Key ClosestDiatonicKey => FindClosestDiatonicKey2() ?? Key.Major.C;

    #region IStaticReadonlyCollection<PitchClassSet> Members

    /// <summary>
    ///     Gets all possible set classes (<see href="https://harmoniousapp.net/p/71/Set-Classes" />)
    ///     <br /><see cref="IReadOnlyCollection{PitchClassSet}" />
    /// </summary>
    public static IReadOnlyCollection<PitchClassSet> Items => AllPitchClassSets.Instance;

    #endregion

    public static PitchClassSet FromId(PitchClassSetId id) => id.ToPitchClassSet();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsPitchClass(int pitchClass) => IsPitchClassInMask(PitchClassMask, pitchClass);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsPitchClassInMask(int mask, int pitchClass) => (mask & (1 << (pitchClass & 0xF))) != 0;

    /// <summary>
    ///     Gets the normal form <see cref="PitchClassSet" />
    /// </summary>
    /// <returns>The normal form <see cref="PitchClassSet" /></returns>
    /// <remarks>
    ///     The normal form of a pitch class set is defined as the most compact, ascending arrangement of pitch classes
    ///     starting from the lowest pitch. This method evaluates all rotations and orderings of the pitch classes within the
    ///     set to determine the smallest interval span that is the most compact.
    ///     <para>Example of calculating the normal form for a G major triad:</para>
    ///     <para>
    ///         <strong>Step 1: Assign Numerical Values</strong><br />
    ///         G = 7, B = 11, D = 2
    ///     </para>
    ///     <para>
    ///         <strong>Step 2: Arrange Numerically and Evaluate Rotations</strong><br />
    ///         Original Order: D (2), G (7), B (11)<br />
    ///         Rotation 1: Intervals - 5, 4<br />
    ///         Rotation 2: Intervals - 4, 3<br />
    ///         Rotation 3: Intervals - 3, 5
    ///     </para>
    ///     <para>
    ///         <strong>Step 3: Transpose Rotations and Check Compactness</strong><br />
    ///         Rotation 1: 2, 7, 11 → 0, 5, 9<br />
    ///         Rotation 2: 7, 11, 14 → 0, 4, 7<br />
    ///         Rotation 3: 11, 14, 19 → 0, 3, 8
    ///     </para>
    ///     <para>
    ///         <strong>Conclusion:</strong><br />
    ///         The most compact form, after transposing and evaluating, is Rotation 3 (0, 3, 8) with the smallest interval
    ///         span (from 0 to 8).
    ///     </para>
    /// </remarks>
    /// <example>
    ///     <code>
    /// PitchClassSet triad = new PitchClassSet(new int[] { 7, 11, 2 });
    /// PitchClassSet normalForm = triad.ToNormalForm();
    /// Console.WriteLine(normalForm);  // Outputs: {0, 3, 8}
    /// </code>
    /// </example>
    /// <remarks>
    ///     Each rotation puts one member on 0; the one kept has the smallest spread between its largest
    ///     and smallest circular gap, ties broken by the lexicographically smaller gap sequence and then
    ///     by the earlier member. This is not the textbook normal form, which minimises the span from the
    ///     first pitch class to the last. The answer for each of the 4,096 sets is computed once, on 12-bit
    ///     masks, and read from <see cref="SetTables.NormalFormMasks" /> afterwards. The set returned is
    ///     shared between callers: it is immutable.
    /// </remarks>
    public PitchClassSet ToNormalForm()
    {
        var mask = SetTables.NormalFormMasks[Id.Value];
        return SetTables.NormalForms[mask] ??= FromId(new(mask));
    }

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
                var closestNote =
                    FindClosestDiatonicNoteWithAccidental(IsModal, usedNaturalNotes, pitchClass, key.Notes);
                notes.Add(closestNote);
            }
        }

        var result = notes.ToImmutableList().AsPrintable();

        return result;
    }

    private static Note.Accidented FindClosestDiatonicNoteWithAccidental(bool isModal,
        HashSet<NaturalNote> usedNaturalNotes,
        PitchClass target,
        IReadOnlyCollection<Note.KeyNote> keyNotes)
    {
        Note.Accidented? closestNote = null;
        var smallestDifference = int.MaxValue;
        foreach (var keyNote in keyNotes)
        {
            var difference = Math.Abs((int)keyNote.PitchClass - (int)target);
            if (difference >= smallestDifference)
            {
                continue;
            }

            smallestDifference = difference;
            closestNote = keyNote;
        }

        if (closestNote == null)
        {
            throw new InvalidOperationException("No closest diatonic note found.");
        }

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
            IsNormalForm ? this : ToNormalForm();
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
            .ThenBy(key => key.KeySignature.AccidentalCount) // then by fewer accidentals
            .ThenByDescending(key => key.KeySignature.IsSharpKey) // prefer sharp keys if tied
            .ThenBy(key => key.KeyMode) // then by key mode
            .ToImmutableList();

        return candidateKeys.FirstOrDefault();
    }

    /// <summary>
    ///     The keys whose seven pitch classes contain this set, fewest accidentals first (ties in
    ///     <see cref="Key.Items" /> order). Computed once per set and shared: the list is immutable.
    /// </summary>
    public IReadOnlyCollection<Key> GetCompatibleKeys()
    {
        var cache = SetTables.CompatibleKeys;
        return cache[Id.Value] ??= SetTables.ComputeCompatibleKeys(Id.Value);
    }

    /// <summary>
    ///     The key sharing the most pitch classes with this set. Ties go to the expected mode (minor when
    ///     the normal form contains pitch class 3), then to the key that comes first in
    ///     <see cref="Key.Items" />.
    /// </summary>
    public Key? FindClosestDiatonicKey2()
    {
        var mask = Id.Value;
        var expectMinor = (SetTables.NormalFormMasks[mask] & (1 << 3)) != 0;

        // Replacing only on a strictly better score, or on an equal score that moves to the expected
        // mode, keeps the first of equals: what OrderByDescending(count).ThenByDescending(mode).First()
        // returned, without the four collections per key it built to get there.
        var keys = SetTables.Keys;
        var best = keys[0];
        var bestScore = -1;
        var bestExpected = false;
        foreach (var candidate in keys)
        {
            var score = BitOperations.PopCount((uint)(mask & candidate.Mask));
            var expected = candidate.IsMinor == expectMinor;
            if (score > bestScore || (score == bestScore && expected && !bestExpected))
            {
                (best, bestScore, bestExpected) = (candidate, score, expected);
            }
        }

        return best.Key;
    }

    /// <summary>
    ///     Tables over the 4,096 twelve-bit sets, built on first use. A set is a 12-bit mask (bit p for
    ///     pitch class p), so transposing is rotating the mask and counting shared pitch classes is a
    ///     population count.
    /// </summary>
    private static class SetTables
    {
        internal static readonly (Key Key, int Mask, bool IsMinor)[] Keys =
        [
            .. Key.Items.Select(key => (key,
                key.Notes.Aggregate(0, (mask, note) => mask | 1 << note.PitchClass.Value),
                key.KeyMode == KeyMode.Minor))
        ];

        internal static readonly int[] NormalFormMasks = [.. Enumerable.Range(0, 4096).Select(NormalFormMask)];

        internal static readonly IReadOnlyCollection<Key>?[] CompatibleKeys = new IReadOnlyCollection<Key>?[4096];

        internal static readonly PitchClassSet?[] NormalForms = new PitchClassSet?[4096];

        internal static IReadOnlyCollection<Key> ComputeCompatibleKeys(int mask) =>
        [
            .. Keys.Where(k => (mask & ~k.Mask) == 0)
                .OrderBy(k => k.Key.KeySignature.AccidentalCount)
                .Select(k => k.Key)
        ];

        private static int Rotr12(int value, int n) => ((value >> n) | (value << (12 - n))) & 0xFFF;

        private static int NormalFormMask(int set)
        {
            if (set == 0)
            {
                return 0;
            }

            Span<int> gaps = stackalloc int[12];
            Span<int> bestGaps = stackalloc int[12];
            var count = BitOperations.PopCount((uint)set);
            var best = 0;
            var bestSpan = int.MaxValue;

            // Members in ascending order, and a rotation replaces the incumbent only when strictly better
            for (var member = 0; member < 12; member++)
            {
                if ((set & (1 << member)) == 0)
                {
                    continue;
                }

                var rotation = Rotr12(set, member);
                Gaps(rotation, count, gaps);
                var span = Span(gaps[..count]);
                if (span > bestSpan || (span == bestSpan && !MoreCompact(gaps[..count], bestGaps[..count])))
                {
                    continue;
                }

                best = rotation;
                bestSpan = span;
                gaps[..count].CopyTo(bestGaps);
            }

            return best;

            // The gap from each member to the next around the circle; a single note's gap to itself is 0
            static void Gaps(int rotation, int count, Span<int> gaps)
            {
                if (count == 1)
                {
                    gaps[0] = 0;
                    return;
                }

                var previous = 0;
                var index = 0;
                for (var pitchClass = 1; pitchClass < 12; pitchClass++)
                {
                    if ((rotation & (1 << pitchClass)) == 0)
                    {
                        continue;
                    }

                    gaps[index++] = pitchClass - previous;
                    previous = pitchClass;
                }

                gaps[index] = 12 - previous;
            }

            static int Span(ReadOnlySpan<int> gaps)
            {
                int min = gaps[0], max = gaps[0];
                foreach (var gap in gaps)
                {
                    min = Math.Min(min, gap);
                    max = Math.Max(max, gap);
                }

                return max - min;
            }

            // The first gap that differs decides; equal sequences are not more compact
            static bool MoreCompact(ReadOnlySpan<int> candidate, ReadOnlySpan<int> incumbent)
            {
                for (var i = 0; i < candidate.Length; i++)
                {
                    if (candidate[i] != incumbent[i])
                    {
                        return candidate[i] < incumbent[i];
                    }
                }

                return false;
            }
        }
    }

    #region Innner Classes

    private class AllPitchClassSets : LazyCollectionBase<PitchClassSet>
    {
        public static readonly AllPitchClassSets Instance = new();

        private AllPitchClassSets() : base(Collection, ", ")
        {
        }

        private static IEnumerable<PitchClassSet> Collection =>
            PitchClassSetId.Items.Select(id => id.ToPitchClassSet());
    }

    #endregion

    #region IParsable<PitchClassSet> Members

    public static PitchClassSet Parse(string s, IFormatProvider? provider = null)
    {
        if (!TryParse(s, null, out var result))
        {
            throw new PitchClassSetParseException();
        }

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
            if (!PitchClass.TryParseSetNotation(segment, out var pitchClass))
            {
                return false; // Fail if one item fails parsing
            }

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
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        return other is null ? 1 : Id.CompareTo(other.Id);
    }

    public static bool operator <(PitchClassSet? left, PitchClassSet? right) =>
        Comparer<PitchClassSet>.Default.Compare(left, right) < 0;

    public static bool operator >(PitchClassSet? left, PitchClassSet? right) =>
        Comparer<PitchClassSet>.Default.Compare(left, right) > 0;

    public static bool operator <=(PitchClassSet? left, PitchClassSet? right) =>
        Comparer<PitchClassSet>.Default.Compare(left, right) <= 0;

    public static bool operator >=(PitchClassSet? left, PitchClassSet? right) =>
        Comparer<PitchClassSet>.Default.Compare(left, right) >= 0;

    #endregion

    #region Equality Members

    private bool Equals(PitchClassSet other) => Id.Equals(other.Id);

    public override bool Equals(object? obj) =>
        ReferenceEquals(this, obj) || (obj is PitchClassSet other && Equals(other));

    public override int GetHashCode() => Id.GetHashCode();

    public static bool operator ==(PitchClassSet? left, PitchClassSet? right) => Equals(left, right);

    public static bool operator !=(PitchClassSet? left, PitchClassSet? right) => !Equals(left, right);

    #endregion

    #region IReadOnlySet members

    public IEnumerator<PitchClass> GetEnumerator() => _pitchClassesSet.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_pitchClassesSet).GetEnumerator();

    public int Count => _pitchClassesSet.Count;

    public bool Contains(PitchClass item) => IsPitchClassInMask(PitchClassMask, item.Value);

    public bool IsProperSubsetOf(IEnumerable<PitchClass> other) => _pitchClassesSet.IsProperSubsetOf(other);

    public bool IsProperSupersetOf(IEnumerable<PitchClass> other) => _pitchClassesSet.IsProperSupersetOf(other);

    public bool IsSubsetOf(IEnumerable<PitchClass> other) => _pitchClassesSet.IsSubsetOf(other);

    public bool IsSupersetOf(IEnumerable<PitchClass> other) => _pitchClassesSet.IsSupersetOf(other);

    public bool Overlaps(IEnumerable<PitchClass> other) => _pitchClassesSet.Overlaps(other);

    public bool SetEquals(IEnumerable<PitchClass> other) => _pitchClassesSet.SetEquals(other);

    #endregion
}
