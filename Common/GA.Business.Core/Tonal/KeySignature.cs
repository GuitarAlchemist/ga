namespace GA.Business.Core.Tonal;

using GA.Business.Core.Notes.Extensions;
using GA.Business.Core.Notes.Primitives;
using GA.Business.Core.Intervals.Primitives;
using KeyNote = Notes.Note.KeyNote;

/// <summary>
/// Key signature (See https://en.wikipedia.org/wiki/Key_signature)
/// </summary>
/// <remarks>
/// Implements <see cref="IRangeValueObject{KeySignature}"/> | <see cref="IStaticReadonlyCollectionFromValues{KeySignature}"/> | <see cref="IReadOnlyCollection{KeyNote}"/>
/// </remarks>
[PublicAPI]
public readonly record struct KeySignature : IStaticReadonlyCollectionFromValues<KeySignature>
{
    #region IStaticReadonlyCollectionFromValues<KeySignature> Members

    public static IReadOnlyCollection<KeySignature> Items => IStaticReadonlyCollectionFromValues<KeySignature>.Items;

    public static KeySignature Min => new(_minValue);
    public static KeySignature Max => new(_maxValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature FromValue([ValueRange(_minValue, _maxValue)] int value) => new(value);
    
    private readonly int _value;

    public int Value { get => _value; init => _value = IRangeValueObject<KeySignature>.EnsureValueInRange(value, _minValue, _maxValue); }
    
    public static implicit operator KeySignature(int value) => new(value);
    public static implicit operator int(KeySignature keySignature) => keySignature.Value;
    
    private const int _minValue = -7;
    private const int _maxValue = 7;
    
    #endregion

    #region Relational members

    public int CompareTo(KeySignature other) => _value.CompareTo(other._value);
    public static bool operator <(KeySignature left, KeySignature right) => left.CompareTo(right) < 0;
    public static bool operator >(KeySignature left, KeySignature right) => left.CompareTo(right) > 0;
    public static bool operator <=(KeySignature left, KeySignature right) => left.CompareTo(right) <= 0;
    public static bool operator >=(KeySignature left, KeySignature right) => left.CompareTo(right) >= 0;

    #endregion

    #region Static Helpers
    
    public static KeySignature Sharp([ValueRange(0, 7)] int count) => new(count);
    public static KeySignature Flat([ValueRange(1, 7)] int count) => new(-count);

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private KeySignature([ValueRange(_minValue, _maxValue)] int value) : this()
    {
        _value = value;

        var accidentedNotes = GetAccidentedNotes(value).AsPrintable();
        AccidentedNotes = accidentedNotes;
        AccidentedNaturalNotesSet = accidentedNotes.Select(note => note.NaturalNote).ToImmutableHashSet().AsPrintable(); 
    }

    /// <summary>
    /// Gets the accidental count <see cref="int"/>
    /// </summary>
    public int AccidentalCount => Math.Abs(_value);

    /// <summary>
    /// Gets the <see cref="PrintableReadOnlyCollection{KeyNote}"/> of accidented notes
    /// </summary>
    public PrintableReadOnlyCollection<KeyNote> AccidentedNotes { get; }

    /// <summary>
    /// Gets the <see cref="PrintableReadOnlySet{NaturalNote}"/> of accidented notes
    /// </summary>
    public PrintableReadOnlySet<NaturalNote> AccidentedNaturalNotesSet { get; }

    /// <summary>
    /// Gets the <see cref="AccidentalKind"/>
    /// </summary>
    public AccidentalKind AccidentalKind => _value < 0 ? AccidentalKind.Flat : AccidentalKind.Sharp;
    
    /// <summary>
    /// True if sharp key, false otherwise
    /// </summary>
    public bool IsSharpKey => _value >= 0;
    
    /// <summary>
    /// True if flat key, false otherwise
    /// </summary>
    public bool IsFlatKey => _value < 0;
    
    /// <summary>
    /// Indicates if the specified <paramref name="naturalNote"/> is accidented
    /// </summary>
    /// <param name="naturalNote">The <see cref="NaturalNote"/></param>
    /// <returns>True if accidented, false otherwise</returns>
    public bool IsNoteAccidented(NaturalNote naturalNote) => AccidentedNaturalNotesSet.Contains(naturalNote);

    /// <inheritdoc />
    public override string ToString() => AccidentedNotes.ToString();
    
    private static ImmutableList<KeyNote> GetAccidentedNotes(int keySignatureValue)
    {
        var count = Math.Abs(keySignatureValue);
        IEnumerable<KeyNote> notes =
            keySignatureValue < 0
                ? GetNotes(NaturalNote.B, count, SimpleIntervalSize.Fourth).ToFlatNotes() // Circle of Fourths, starting from B
                : GetNotes(NaturalNote.F, count, SimpleIntervalSize.Fifth).ToSharpNotes(); // Circle of Fifths, starting from F
        return notes.ToImmutableList();

        static IEnumerable<NaturalNote> GetNotes(NaturalNote firstItem, int count, SimpleIntervalSize increment)
        {
            var item = firstItem;
            for (var i = 0; i < count; i++)
            {
                yield return item;
                item += increment;
            }
        }
    }
}