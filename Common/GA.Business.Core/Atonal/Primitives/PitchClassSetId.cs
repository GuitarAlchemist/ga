namespace GA.Business.Core.Atonal.Primitives;

using Notes;

/// <summary>
/// A pitch class set ID
/// </summary>
/// <remarks>
///  12 tones of the chromatic scale arranged as sets<br/>
///  Each note is included or excluded in a set (0 = omitted, 1 = included)
///  2^12 => 4096 combinations
/// </remarks>
[PublicAPI]
public readonly record struct PitchClassSetId : IStaticReadonlyCollectionFromValues<PitchClassSetId>
{
    #region IStaticReadonlyCollectionFromValues<PitchClassSetId> Members

    public static implicit operator PitchClassSetId(int value) => new() { Value = value };
    public static implicit operator int(PitchClassSetId pitchClassSetId) => pitchClassSetId._value;

    /// <summary>
    /// Gets all possible 4096 <see cref="PitchClassSetId"/> items
    /// </summary>
    public static IReadOnlyCollection<PitchClassSetId> Items => IStaticReadonlyCollectionFromValues<PitchClassSetId>.Items;

    /// <inheritdoc />
    public static PitchClassSetId Min => FromValue(_minValue);

    /// <inheritdoc />
    public static PitchClassSetId Max => FromValue(_maxValue);

    /// <inheritdoc />
    public static PitchClassSetId FromValue([ValueRange(_minValue, _maxValue)] int value) => new() { Value = value };

    /// <inheritdoc />
    public int Value
    {
        get => _value;
        private init => _value = ValueObjectUtils<PitchClassSetId>.CheckRange(value, _minValue, _maxValue);
    }

    private const int _minValue = 0;
    private const int _maxValue = 4095;
    private readonly int _value;
    
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

        var result = new PitchClassSetId
        {
            Value = GetBase12Value(
                pitchClasses as IReadOnlySet<PitchClass>
                ??
                pitchClasses.ToImmutableHashSet()
            )
        };
        return result;

        static int GetBase12Value(IReadOnlySet<PitchClass> pitchClasses)
        {
            var value = 0;
            var index = 0;
            
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var pitchClass in PitchClass.Items)
            {
                var weight = 1 << index++;
                if (pitchClasses.Contains(pitchClass)) value += weight;
            }

            return value;
        }
    }    
    
    #endregion

    #region Equality Members

    /// <inheritdoc />
    public bool Equals(PitchClassSetId other) => _value == other._value;

    /// <inheritdoc />
    public override int GetHashCode() => _value;

    #endregion

    #region Relational Members

    public static bool operator <(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) < 0;
    public static bool operator >(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) > 0;
    public static bool operator <=(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) <= 0;
    public static bool operator >=(PitchClassSetId left, PitchClassSetId right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public int CompareTo(PitchClassSetId other) => _value.CompareTo(other._value);

    #endregion

    /// <summary>
    /// Gets chromatic notes for the Pitch Class Set ID
    /// </summary>
    /// <returns>The <see cref="ImmutableSortedSet{T}"/> where T is a <see cref="Note.Chromatic"/></returns>
    public ImmutableSortedSet<Note.Chromatic> GetChromaticNotes() => GetChromaticNotesInternal(this);

    private static ImmutableSortedSet<Note.Chromatic> GetChromaticNotesInternal(PitchClassSetId id)
    {
        var builder = ImmutableSortedSet.CreateBuilder<Note.Chromatic>();
        var runningValue = id.Value;
        var noteValue = 0;

        while (runningValue != 0)
        {
            var addNote = (runningValue & 1) == 1;
            if (addNote) builder.Add(noteValue);
            
            // Prepare for next note
            runningValue >>= 1;
            noteValue++;
        }
        return builder.ToImmutable();
    }
}