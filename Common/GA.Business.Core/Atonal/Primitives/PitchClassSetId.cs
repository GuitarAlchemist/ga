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

    public static implicit operator PitchClassSetId(int value) => new(value);
    public static implicit operator int(PitchClassSetId pitchClassSetId) => pitchClassSetId.Value;

    /// <summary>
    /// Gets all possible 4096 <see cref="PitchClassSetId"/> items
    /// </summary>
    public static IReadOnlyCollection<PitchClassSetId> Items => IStaticReadonlyCollectionFromValues<PitchClassSetId>.Items;

    /// <inheritdoc />
    public static PitchClassSetId Min => FromValue(_minValue);

    /// <inheritdoc />
    public static PitchClassSetId Max => FromValue(_maxValue);

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
    
    public PitchClassSetId(int value)
    {
        Value = ValueObjectUtils<PitchClassSetId>.CheckRange(value, _minValue, _maxValue);
    }

    /// <summary>
    /// Gets chromatic notes
    /// </summary>
    /// <returns>The <see cref="ImmutableSortedSet{T}"/> where T is a <see cref="Note.Chromatic"/></returns>
    public ImmutableSortedSet<Note.Chromatic> Notes => GetNotesInternal(Value);

    /// <summary>
    /// Get the complement <see cref="PitchClassSetId"/>
    /// </summary>
    public PitchClassSetId Complement => new(Value ^ 0b111111111111);

    /// <inheritdoc />
    public override string ToString() => $"{Value} ({PrintValueBinary(Value)}; {PrintNotes(Notes)})";

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
    
    private static string PrintValueBinary(int value) => Convert.ToString(value, 2).PadLeft(12, '0');
    private static string PrintNotes(ImmutableSortedSet<Note.Chromatic> notes) => string.Join(" ", notes.Select(n => n.ToString()));
}