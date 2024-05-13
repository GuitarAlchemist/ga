namespace GA.Business.Core.Atonal.Primitives;

using Notes;
using Notes.Extensions;

/// <summary>
/// A pitch class set ID
/// </summary>
/// <remarks>
///  12 tones of the chromatic scale arranged as sets<br/>
///  Each note is included or excluded in a set (0 = omitted, 1 = included)
///  2^12 => 4096 combinations
///
/// IStaticReadonlyCollectionFromValues level - derives from <see cref="IStaticReadonlyCollection{TSelf}"/> | <see cref="IRangeValueObject{TSelf}"/>
/// IValueObject level - derives from <see cref="IComparable{TSelf}"/> | <see cref="IEquatable{TSelf}"/>
/// </remarks>
[PublicAPI]
public readonly record struct PitchClassSetId : IStaticReadonlyCollectionFromValues<PitchClassSetId>
{
    private static readonly Lazy<ImmutableDictionary<int, PitchClassSetId>> _lazyByValue;

    static PitchClassSetId()
    {
        Items = IStaticReadonlyCollectionFromValues<PitchClassSetId>.Items;
        _lazyByValue = new Lazy<ImmutableDictionary<int, PitchClassSetId>>(() => Items.ToImmutableDictionary(id => id.Value));
    }

    #region Equality Comparers

    /// <summary>
    /// Gets the default <see cref="IEqualityComparer{PitchClassSetId}"/>
    /// </summary>
    public static IEqualityComparer<PitchClassSetId> DefaultComparer { get; } = new ValueEqualityComparer();

    /// <summary>
    /// Gets the <see cref="IEqualityComparer{PitchClassSetId}"/> for <see cref="Complement"/> property
    /// </summary>
    public static IEqualityComparer<PitchClassSetId> ComplementComparer { get; } = new ComplementEqualityComparer();

    /// <summary>
    /// Equality comparer where complementary Pitch Class Set IDs are considered equal
    /// </summary>
    /// <remarks>
    /// e.g. 000010010001 binary value (145 integer value) vs 111101101110 binary value (3950 integer value)
    /// </remarks>
    private sealed class ComplementEqualityComparer : IEqualityComparer<PitchClassSetId>
    {
        /// <inheritdoc />
        public bool Equals(PitchClassSetId x, PitchClassSetId y) => x.Value == y.Value || x.Value == (y.Complement.Value);

        /// <inheritdoc />
        public int GetHashCode(PitchClassSetId obj)
        {
            // Calculate hash code based on the canonical smaller value between a set and its complement
            var complementValue = obj.Complement.Value;
            var canonicalValue = Math.Min(obj.Value, complementValue);
            return canonicalValue.GetHashCode();
        }
    }

    private sealed class ValueEqualityComparer : IEqualityComparer<PitchClassSetId>
    {
        public bool Equals(PitchClassSetId x, PitchClassSetId y) => x.Value == y.Value;
        public int GetHashCode(PitchClassSetId obj) => obj.Value;
    }

    #endregion

    #region Static Helpers

    /// <summary>
    /// Creates a Pitch Class Set ID from pitch classes
    /// </summary>
    /// <param name="pitchClasses">The <see cref="IEnumerable{PitchClass}"/></param>
    /// <returns>The <see cref="PitchClassSetIdentity"/></returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pitchClasses"/> is null</exception>
    public static PitchClassSetId FromPitchClasses(IEnumerable<PitchClass> pitchClasses)
    {
        ArgumentNullException.ThrowIfNull(pitchClasses);

        var pitchClassesSet =
            pitchClasses as IReadOnlySet<PitchClass>
            ??
            pitchClasses.ToImmutableHashSet();
        var value = GetBase12Value(pitchClassesSet);
        var result = new PitchClassSetId(value);
        return result;

        static int GetBase12Value(IReadOnlySet<PitchClass> pitchClassesSet)
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

    #endregion

    #region IStaticReadonlyCollectionFromValues<PitchClassSetId> Members

    public static implicit operator PitchClassSetId(int value) => _lazyByValue.Value[value];
    public static implicit operator int(PitchClassSetId pitchClassSetId) => pitchClassSetId.Value;

    /// <summary>
    /// Gets all possible 4096 <see cref="PitchClassSetId"/> items
    /// </summary>
    public static IReadOnlyCollection<PitchClassSetId> Items { get; }

    /// <inheritdoc />
    public static PitchClassSetId Min => new(_minValue);

    /// <inheritdoc />
    public static PitchClassSetId Max => new(_maxValue);

    /// <inheritdoc />
    public static PitchClassSetId FromValue([ValueRange(_minValue, _maxValue)] int value) => new(value);

    /// <inheritdoc />
    public int Value { get; }

    private const int _minValue = 0;
    private const int _maxValue = 4095;

    #endregion

    #region Equality Members

    /// <inheritdoc />
    public bool Equals(PitchClassSetId other) => Value == other.Value;

    /// <inheritdoc />
    public override int GetHashCode() => Value;

    #endregion

    #region Relational Members

    public static bool operator <(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) < 0;
    public static bool operator >(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) > 0;
    public static bool operator <=(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public int CompareTo(PitchClassSetId other) => Value.CompareTo(other.Value);

    #endregion

    /// <summary>
    /// Creates a <see cref="PitchClassSetId"/> instance
    /// </summary>
    /// <param name="value">The <see cref="Int32"/> value</param>
    public PitchClassSetId(int value) => Value = ValueObjectUtils<PitchClassSetId>.CheckRange(value, _minValue, _maxValue);

    /// <summary>
    /// Gets chromatic notes
    /// </summary>
    /// <returns>The <see cref="ChromaticNoteSet"/></returns>
    public ChromaticNoteSet Notes => GetNotesInternal(Value).ToChromaticNoteSet();

    /// <summary>
    /// Gets the binary representation of the Pitch Class ID value <see cref="string"/>
    /// </summary>
    public string BinaryValue => Convert.ToString(Value, 2).PadLeft(12, '0');

    /// <summary>
    /// Get the complement Pitch Class ID
    /// </summary>
    /// <remarks>
    /// A complement operation "Flips" each note (i.e. An note included in the source set is excluded in the resulting set, and vice versa)
    /// </remarks>
    public PitchClassSetId Complement => new(Value ^ 0b111111111111);

    /// <summary>
    /// Gets the inversion Pitch Class ID
    /// </summary>
    /// <remarks>
    /// An inverse operation mirrors the notes arranged on a circle (i.e. Vertical axis symmetry for each included note)
    /// </remarks>
    public PitchClassSetId Inverse => new(MirrorValue(Value));

    /// <summary>
    /// Gets rotated Pitch Class ID
    /// </summary>
    /// <param name="count">The rotation <see cref="Int32"/> amount</param>
    /// <returns>The <see cref="PitchClassSetId"/></returns>
    public PitchClassSetId Rotate(int count) => new(RotateValue(Value, count));

    /// <summary>
    /// Get the complement of an existing Pitch Class ID
    /// </summary>
    /// <param name="id">The <see cref="PitchClassSetId"/></param>
    /// <returns>The complement <see cref="PitchClassSetId"/></returns>
    public static PitchClassSetId operator !(PitchClassSetId id) => id.Complement;

    /// <summary>
    /// Get the Pitch Class Set object
    /// </summary>
    /// <returns>The <see cref="PitchClassSet"/></returns>
    public PitchClassSet PitchClassSet => new(Notes.PitchClassCollection);

    /// <summary>
    /// Geta all rotations, including the current Pitch Class Set ID
    /// </summary>
    /// <returns></returns>
    public IEnumerable<PitchClassSetId> GetRotations()
    {
        for (var i = 0; i < 12; i++)
        {
            yield return Rotate(i);
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{Value} ({BinaryValue}; {Notes})";

    private static int RotateValue(int value, int count)
    {
        count = Math.Abs(count) % 12; // Normalize count
        var result = ((value << count) | (value >> (12 - count))) & 0xFFF;
        return result;
    }

    private static int MirrorValue(int value)
    {
        var result = 0;

        // Iterate through each bit from 0 to 11
        for (var i = 0; i < 12; i++)
        {
            // Determine the new position for each bit
            // For example, mirroring around the axis at 6 would swap positions 0 and 11, 1 and 10, etc.
            var bitPosition = (12 - i) % 12;

            // Set the bit at the new position if it's set in the original
            if ((value & (1 << i)) != 0) result |= (1 << bitPosition);
        }

        return result;
    }

    private static ImmutableSortedSet<Note.Chromatic> GetNotesInternal(int value)
    {
        var builder = ImmutableSortedSet.CreateBuilder<Note.Chromatic>();
        var runningValue = value;
        var noteValue = 0;

        while (runningValue != 0)
        {
            var addNote = (runningValue & 1) == 1;
            if (addNote)
            {
                var note = new Note.Chromatic(noteValue);
                builder.Add(note);
            }

            // Prepare for next note
            runningValue >>= 1;
            noteValue++;
        }
        return builder.ToImmutable();
    }
}