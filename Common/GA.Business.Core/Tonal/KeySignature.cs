using GA.Business.Core.Intervals;

namespace GA.Business.Core.Tonal;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using GA.Core;
using GA.Business.Core.Notes.Primitives;

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
    private readonly Lazy<IReadOnlySet<NaturalNote>> _lazyAccidentedNotes;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]

    private KeySignature([ValueRange(_minValue, _maxValue)] int value) : this()
    {
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
    public IReadOnlyCollection<NaturalNote> FlatNotes => GetFlatNotes(Value).AsPrintable();
    public IReadOnlyCollection<NaturalNote> AccidentedNotes => _lazyAccidentedNotes.Value;

    private static IReadOnlySet<NaturalNote> GetAccidentedNotes(int value)
    {
        var accidentedNotes = value < 0 ? GetSharpNotes(value) : GetFlatNotes(value);
        var result = accidentedNotes.ToImmutableHashSet().AsPrintable();

        return result;
    }

    public static KeySignature Sharp([ValueRange(0, 7)] int count) => new(count);
    public static KeySignature Flat([ValueRange(1, 7)] int count) => new(-count);

    public bool Contains(NaturalNote note) => AccidentedNotes.Contains(note);

    private static IReadOnlyCollection<NaturalNote> GetSharpNotes(int value)
    {
        var result =
            value <= 0
                ? ImmutableList<NaturalNote>.Empty
                : GetNotes(NaturalNote.F, value, 4).ToImmutableList(); // See https://en.wikipedia.org/wiki/Circle_of_fifths

        return result;
    }

    private static IReadOnlyCollection<NaturalNote> GetFlatNotes(int value)
    {
        var result =
            value >= 0
                ? ImmutableList<NaturalNote>.Empty
                : GetNotes(NaturalNote.B, -value,3).ToImmutableList(); // See https://en.wikipedia.org/wiki/Circle_of_fifths

        return result;
    }

    private static IEnumerable<NaturalNote> GetNotes(
        NaturalNote firstItem,
        int accidentalCount,
        int degreeCount)
    {
        var item = firstItem;
        for (var i = 0; i < accidentalCount; i++)
        {
            yield return item;

            item = item.ToDegree(degreeCount);
        }
    }
}