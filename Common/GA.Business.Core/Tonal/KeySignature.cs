namespace GA.Business.Core.Tonal;

using Notes;
using GA.Business.Core.Notes.Primitives;
using GA.Business.Core.Intervals.Primitives;
using Intervals;
using GA.Core.Extensions;

/// <summary>
/// Key signature (See https://en.wikipedia.org/wiki/Key_signature)
/// </summary>
[PublicAPI]
public readonly record struct KeySignature : IValueObject<KeySignature>, 
                                             IReadOnlyCollection<Note.KeyNote>
{
    #region Relational members

    public int CompareTo(KeySignature other) => _value.CompareTo(other._value);
    public static bool operator <(KeySignature left, KeySignature right) => left.CompareTo(right) < 0;
    public static bool operator >(KeySignature left, KeySignature right) => left.CompareTo(right) > 0;
    public static bool operator <=(KeySignature left, KeySignature right) => left.CompareTo(right) <= 0;
    public static bool operator >=(KeySignature left, KeySignature right) => left.CompareTo(right) >= 0;

    #endregion

    private const int _minValue = -7;
    private const int _maxValue = 7;
    private readonly Lazy<IReadOnlyCollection<Note.KeyNote>> _lazyAccidentedNotes;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static KeySignature FromValue([ValueRange(_minValue, _maxValue)] int value) => new(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private KeySignature([ValueRange(_minValue, _maxValue)] int value) : this()
    {
        _value = value;
        _lazyAccidentedNotes = new(() => GetAccidentedNotes(value));
    }

    public static int CheckRange(int value) => ValueObjectUtils<KeySignature>.CheckRange(value, _minValue, _maxValue);    

    public static KeySignature Min => new(_minValue);
    public static KeySignature Max => new(_maxValue);
    public static implicit operator KeySignature(int value) => new(value);
    public static implicit operator int(KeySignature keySignature) => keySignature.Value;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public AccidentalKind AccidentalKind => _value < 0 ? AccidentalKind.Flat : AccidentalKind.Sharp;
    public bool IsSharpKey => _value >= 0;
    public bool IsFlatKey => _value < 0;
    public IReadOnlyCollection<Note.KeyNote> SignatureNotes => _lazyAccidentedNotes.Value;

    private static IReadOnlyCollection<Note.KeyNote> GetAccidentedNotes(int value)
    {
        var count = Math.Abs(value);
        var notes = 
            value < 0 
                ? GetFlatNotes(count).Cast<Note.KeyNote>() 
                : GetSharpNotes(count);

        var result = notes.ToImmutableList().AsPrintable();

        return result;
    }

    public static KeySignature Sharp([ValueRange(0, 7)] int count) => new(count);
    public static KeySignature Flat([ValueRange(1, 7)] int count) => new(-count);

    public bool Contains(NaturalNote note) => SignatureNotes.Any(keyNote => keyNote.NaturalNote == note);

    public IEnumerator<Note.KeyNote> GetEnumerator() => _lazyAccidentedNotes.Value.GetEnumerator();
    public override string ToString() => _lazyAccidentedNotes.Value.ToString()!;
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _lazyAccidentedNotes.Value).GetEnumerator();

    private static IReadOnlyCollection<Note.SharpKey> GetSharpNotes(int count)
    {
        var result =
            GetNotes(NaturalNote.F, count, IntervalSize.Fifth)
                .Select(note => new Note.SharpKey(note, SharpAccidental.Sharp))
                .ToImmutableList()
                .AsPrintable();// See https://en.wikipedia.org/wiki/Circle_of_fifths

        return result;
    }

    private static IReadOnlyCollection<Note.FlatKey> GetFlatNotes(int count)
    {
        var result =
            GetNotes(NaturalNote.B, count, IntervalSize.Fourth)
                .Select(note => new Note.FlatKey(note, FlatAccidental.Flat))
                .ToImmutableList()
                .AsPrintable(); // See https://en.wikipedia.org/wiki/Circle_of_fifths

        return result;
    }

    private static IEnumerable<NaturalNote> GetNotes(
        NaturalNote firstItem,
        int count,
        IntervalSize increment)
    {
        var item = firstItem;
        for (var i = 0; i < count; i++)
        {
            yield return item;

            item += increment;
        }
    }

    public int Count => _lazyAccidentedNotes.Value.Count;
}