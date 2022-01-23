namespace GA.Business.Core.Tonal;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;

using GA.Core;
using GA.Business.Core.Notes.Primitives;
using GA.Business.Core.Intervals.Primitives;
using Intervals;

/// <summary>
/// Key signature (See https://en.wikipedia.org/wiki/Key_signature)
/// </summary>
[PublicAPI]
public readonly record struct KeySignature : IValue<KeySignature>
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
    private readonly Lazy<IReadOnlyCollection<NaturalNote>> _lazyAccidentedNotes;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    private KeySignature([ValueRange(_minValue, _maxValue)] int value) : this()
    {
        _value = value;
        _lazyAccidentedNotes = new(() => GetAccidentedNotes(value));
    }

    public static int CheckRange(int value) => ValueUtils<KeySignature>.CheckRange(value, _minValue, _maxValue);    

    public static KeySignature Min => new(_minValue);
    public static KeySignature Max => new(_maxValue);
    public static implicit operator KeySignature(int value) => new(value);
    public static implicit operator int(KeySignature keySignature) => keySignature.Value;

    private readonly int _value;
    public int Value { get => _value; init => _value = CheckRange(value); }

    public AccidentalKind AccidentalKind => _value < 0 ? AccidentalKind.Flat : AccidentalKind.Sharp;
    public bool IsSharpKey => _value >= 0;
    public bool IsFlatKey => _value < 0;
    public IReadOnlyCollection<NaturalNote> SharpNotes => GetSharpNotes(Value).AsPrintable();
    public IReadOnlyCollection<NaturalNote> FlatNotes => GetFlatNotes(-Value).AsPrintable();
    public IReadOnlyCollection<NaturalNote> AccidentedNotes => _lazyAccidentedNotes.Value;

    private static IReadOnlyCollection<NaturalNote> GetAccidentedNotes(int value)
    {
        var count = Math.Abs(value);
        var accidentedNotes = value < 0 ? GetFlatNotes(count) : GetSharpNotes(count);
        var result = accidentedNotes.AsPrintable();

        return result;
    }

    public static KeySignature Sharp([ValueRange(0, 7)] int count) => new(count);
    public static KeySignature Flat([ValueRange(1, 7)] int count) => new(-count);

    public bool Contains(NaturalNote note) => AccidentedNotes.Contains(note);

    public override string ToString()
    {
        if (Value == 0) return string.Empty;
        return _value < 0 ? $"{-Value} flat(s)" : $"{Value} sharp(s)";
    }

    private static IReadOnlyCollection<NaturalNote> GetSharpNotes(int count)
    {
        var result =
            GetNotes(NaturalNote.F, count, DiatonicNumber.Fifth).ToImmutableList(); // See https://en.wikipedia.org/wiki/Circle_of_fifths

        return result;
    }

    private static IReadOnlyCollection<NaturalNote> GetFlatNotes(int count)
    {
        var result = 
            GetNotes(NaturalNote.B, count, DiatonicNumber.Fourth).ToImmutableList(); // See https://en.wikipedia.org/wiki/Circle_of_fifths

        return result;
    }

    private static IEnumerable<NaturalNote> GetNotes(
        NaturalNote firstItem,
        int count,
        DiatonicNumber increment)
    {
        var item = firstItem;
        for (var i = 0; i < count; i++)
        {
            yield return item;

            item = item.ToDegree(increment.Value - 1);
        }
    }
}